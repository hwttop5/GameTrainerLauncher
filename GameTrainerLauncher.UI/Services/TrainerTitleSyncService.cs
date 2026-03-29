using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Interfaces;
using GameTrainerLauncher.Core.Models;
using GameTrainerLauncher.Core.Utilities;
using GameTrainerLauncher.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace GameTrainerLauncher.UI.Services;

public class TrainerTitleSyncService : ITrainerTitleSyncService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly TimeSpan SyncInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan MatchedRefreshInterval = TimeSpan.FromDays(14);
    private static readonly TimeSpan UnmatchedRefreshInterval = TimeSpan.FromDays(7);
    private const int MetadataBatchSize = 6;
    private const int GamerskyDetailBatchSize = 16;
    private const int FailedRetryMaxRows = 120;
    private const int SteamFallbackBatchSize = 4;
    private const int SteamFallbackMaxRows = 80;
    private const int SteamBackfillStopConsecutiveErrors = 3;
    private const int SaveInterval = 50;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IScraperService _scraperService;
    private readonly IGameTitleMetadataService _gameTitleMetadataService;
    private readonly ISteamStoreMetadataService _steamStoreMetadataService;
    private readonly IAppSettingsService _appSettingsService;
    private readonly ITrainerTitleSnapshotService _trainerTitleSnapshotService;
    private readonly SemaphoreSlim _syncGate = new(1, 1);

    public TrainerTitleSyncService(
        IServiceScopeFactory scopeFactory,
        IScraperService scraperService,
        IGameTitleMetadataService gameTitleMetadataService,
        ISteamStoreMetadataService steamStoreMetadataService,
        IAppSettingsService appSettingsService,
        ITrainerTitleSnapshotService trainerTitleSnapshotService)
    {
        _scopeFactory = scopeFactory;
        _scraperService = scraperService;
        _gameTitleMetadataService = gameTitleMetadataService;
        _steamStoreMetadataService = steamStoreMetadataService;
        _appSettingsService = appSettingsService;
        _trainerTitleSnapshotService = trainerTitleSnapshotService;
    }

    public async Task EnsureSynchronizedAsync(bool force = false, CancellationToken cancellationToken = default)
    {
        await _syncGate.WaitAsync(cancellationToken);
        try
        {
            var snapshot = _appSettingsService.GetSettings();
            var now = DateTimeOffset.UtcNow;

            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync(cancellationToken);
            await db.EnsureTrainerTitleIndexSchemaAsync(cancellationToken);

            var existingRows = await db.TrainerTitleIndexEntries.ToListAsync(cancellationToken);
            var hasRows = existingRows.Count > 0;
            if (!force &&
                hasRows &&
                snapshot.LastTrainerTitleSyncUtc.HasValue &&
                now - snapshot.LastTrainerTitleSyncUtc.Value < SyncInterval)
            {
                return;
            }

            var catalogEntries = await _scraperService.GetTrainerCatalogEntriesAsync();
            var rowByUrl = existingRows.ToDictionary(row => row.TrainerPageUrl, StringComparer.OrdinalIgnoreCase);
            var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var catalogEntry in catalogEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();

                seenUrls.Add(catalogEntry.TrainerPageUrl);
                if (!rowByUrl.TryGetValue(catalogEntry.TrainerPageUrl, out var row))
                {
                    row = new TrainerTitleIndexEntry
                    {
                        FlingTitle = catalogEntry.Title,
                        TrainerPageUrl = catalogEntry.TrainerPageUrl,
                        NormalizedFlingTitle = TitleSearchNormalizer.NormalizeFlingTitle(catalogEntry.Title)
                    };
                    db.TrainerTitleIndexEntries.Add(row);
                    rowByUrl[catalogEntry.TrainerPageUrl] = row;
                    existingRows.Add(row);
                }

                row.FlingTitle = catalogEntry.Title;
                row.NormalizedFlingTitle = TitleSearchNormalizer.NormalizeFlingTitle(catalogEntry.Title);
                row.IsActive = true;
                row.LastSeenUtc = now.UtcDateTime;
            }

            foreach (var row in existingRows)
            {
                if (!seenUrls.Contains(row.TrainerPageUrl))
                {
                    row.IsActive = false;
                }
            }

            await db.SaveChangesAsync(cancellationToken);

            var localAppIds = await db.Games
                .Where(game => !string.IsNullOrWhiteSpace(game.AppId))
                .Select(game => new { game.Name, game.AppId })
                .ToListAsync(cancellationToken);
            var appIdByNormalizedTitle = localAppIds
                .GroupBy(game => TitleSearchNormalizer.NormalizeEnglishTitle(game.Name))
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .ToDictionary(group => group.Key, group => group.First().AppId!, StringComparer.Ordinal);

            var rowsToRefresh = existingRows
                .Where(row => row.IsActive && ShouldRefreshMetadata(row, force))
                .ToList();

            var updatedCount = 0;
            var matchedCount = 0;
            var steamFallbackCandidates = new List<TrainerTitleIndexEntry>();
            var failedGamerskyRows = new List<TrainerTitleIndexEntry>();
            foreach (var batch in ChunkRows(rowsToRefresh, MetadataBatchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var tasks = batch.Select(row => ResolveGamerskyMetadataAsync(
                    row.FlingTitle,
                    cancellationToken)).ToArray();
                var resolutions = await Task.WhenAll(tasks);

                for (var index = 0; index < batch.Count; index++)
                {
                    var resolution = resolutions[index];
                    if (resolution == null)
                    {
                        failedGamerskyRows.Add(batch[index]);
                        continue;
                    }

                    ApplyResolution(batch[index], resolution, now.UtcDateTime);
                    updatedCount++;
                    if (resolution.MatchStatus == TrainerTitleIndexEntry.MatchStatusMatched)
                    {
                        matchedCount++;
                    }
                    else
                    {
                        steamFallbackCandidates.Add(batch[index]);
                    }
                }

                if (updatedCount > 0 && updatedCount % SaveInterval == 0)
                {
                    await db.SaveChangesAsync(cancellationToken);
                }
            }

            foreach (var batch in ChunkRows(failedGamerskyRows.Take(FailedRetryMaxRows).ToList(), MetadataBatchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var tasks = batch.Select(row => ResolveGamerskyMetadataAsync(row.FlingTitle, cancellationToken)).ToArray();
                var resolutions = await Task.WhenAll(tasks);
                for (var index = 0; index < batch.Count; index++)
                {
                    var resolution = resolutions[index];
                    if (resolution == null)
                    {
                        continue;
                    }

                    ApplyResolution(batch[index], resolution, now.UtcDateTime);
                    updatedCount++;
                    if (resolution.MatchStatus == TrainerTitleIndexEntry.MatchStatusMatched)
                    {
                        matchedCount++;
                    }
                }

                if (updatedCount > 0 && updatedCount % SaveInterval == 0)
                {
                    await db.SaveChangesAsync(cancellationToken);
                }
            }

            var steamFallbackRows = steamFallbackCandidates
                .Take(SteamFallbackMaxRows)
                .ToList();
            foreach (var batch in ChunkRows(steamFallbackRows, SteamFallbackBatchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var tasks = batch.Select(row => ResolveSteamFallbackAsync(
                    row.FlingTitle,
                    TryGetLocalAppId(row, appIdByNormalizedTitle),
                    cancellationToken)).ToArray();
                var resolutions = await Task.WhenAll(tasks);

                for (var index = 0; index < batch.Count; index++)
                {
                    var resolution = resolutions[index];
                    if (resolution == null ||
                        resolution.MatchStatus != TrainerTitleIndexEntry.MatchStatusMatched)
                    {
                        continue;
                    }

                    ApplyResolution(batch[index], resolution, now.UtcDateTime);
                    updatedCount++;
                    matchedCount++;
                }

                if (updatedCount > 0 && updatedCount % SaveInterval == 0)
                {
                    await db.SaveChangesAsync(cancellationToken);
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            _appSettingsService.Update(settings => settings.LastTrainerTitleSyncUtc = now);
            if (updatedCount > 0)
            {
                _ = await _trainerTitleSnapshotService.SaveSnapshotFromDatabaseAsync(db, cancellationToken);
            }
            Logger.Info(
                "Trainer title index sync completed. Active rows: {ActiveCount}, refreshed rows: {UpdatedCount}, matched rows: {MatchedCount}.",
                catalogEntries.Count,
                updatedCount,
                matchedCount);
        }
        finally
        {
            _syncGate.Release();
        }
    }

    public async Task EnsureKeywordSynchronizedAsync(string keyword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return;
        }

        await _syncGate.WaitAsync(cancellationToken);
        try
        {
            var normalizedKeyword = TitleSearchNormalizer.NormalizeChineseTitle(keyword);
            var candidates = await _gameTitleMetadataService.SearchCandidatesAsync(keyword, cancellationToken);
            if (candidates.Count == 0)
            {
                return;
            }

            var metadataEntries = await LoadGamerskyMetadataBatchAsync(candidates, cancellationToken);
            if (metadataEntries.Count == 0)
            {
                return;
            }
            var metadataEnglishKeys = metadataEntries
                .Select(item => TitleSearchNormalizer.NormalizeEnglishTitle(item.EnglishName))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync(cancellationToken);
            await db.EnsureTrainerTitleIndexSchemaAsync(cancellationToken);

            var rows = await db.TrainerTitleIndexEntries
                .Where(row => row.IsActive)
                .ToListAsync(cancellationToken);

            var now = DateTime.UtcNow;
            var updatedCount = 0;
            var unresolved = new List<TrainerTitleIndexEntry>();
            foreach (var row in rows)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var keywordMatch = !string.IsNullOrWhiteSpace(normalizedKeyword) &&
                                   (row.NormalizedChineseName?.Contains(normalizedKeyword, StringComparison.Ordinal) ?? false);
                var englishCandidateMatch = metadataEnglishKeys.Any(key =>
                    row.NormalizedFlingTitle.Contains(key, StringComparison.Ordinal) ||
                    (row.NormalizedEnglishName?.Contains(key, StringComparison.Ordinal) ?? false));
                if (!keywordMatch && !englishCandidateMatch)
                {
                    continue;
                }

                var matchedMetadata = TrainerTitleMatchResolver.TrySelectBestMetadata(row.FlingTitle, metadataEntries);
                if (matchedMetadata != null)
                {
                    var metadata = matchedMetadata.Candidate;
                    ApplyResolution(row, CreateMatchedResolution(
                        metadata.EnglishName,
                        metadata.ChineseName,
                        null,
                        metadata.Source,
                        metadata.SourceUrl,
                        matchedMetadata.Confidence), now);
                    updatedCount++;
                }
                else
                {
                    unresolved.Add(row);
                }
            }

            foreach (var row in unresolved.Take(16))
            {
                var resolution = await ResolveSteamFallbackAsync(row.FlingTitle, row.SteamAppId, cancellationToken);
                if (resolution == null || resolution.MatchStatus != TrainerTitleIndexEntry.MatchStatusMatched)
                {
                    continue;
                }

                ApplyResolution(row, resolution, now);
                updatedCount++;
            }

            if (updatedCount == 0)
            {
                return;
            }

            await db.SaveChangesAsync(cancellationToken);
            _ = await _trainerTitleSnapshotService.SaveSnapshotFromDatabaseAsync(db, cancellationToken);
            Logger.Info("Keyword title sync completed for {Keyword}. Updated rows: {Count}.", keyword, updatedCount);
        }
        finally
        {
            _syncGate.Release();
        }
    }

    public async Task<SteamBackfillReport> EnsureSteamBackfillUntilErrorAsync(int? maxRows = null, CancellationToken cancellationToken = default)
    {
        await _syncGate.WaitAsync(cancellationToken);
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.EnsureCreatedAsync(cancellationToken);
            await db.EnsureTrainerTitleIndexSchemaAsync(cancellationToken);

            var orderedQuery = db.TrainerTitleIndexEntries
                .Where(row => row.IsActive && row.MatchStatus == TrainerTitleIndexEntry.MatchStatusUnmatched)
                .OrderBy(row => row.LastValidatedUtc == null ? 0 : 1)
                .ThenBy(row => row.LastValidatedUtc);
            var rowsQuery = (maxRows.HasValue && maxRows.Value > 0)
                ? orderedQuery.Take(maxRows.Value)
                : orderedQuery;

            var rows = await rowsQuery.ToListAsync(cancellationToken);
            if (rows.Count == 0)
            {
                return new SteamBackfillReport(0, 0, 0, 0, false);
            }

            var now = DateTime.UtcNow;
            var processed = 0;
            var matched = 0;
            var errors = 0;
            var consecutiveErrors = 0;
            var stoppedByErrors = false;
            var pendingSaves = 0;
            foreach (var batch in ChunkRows(rows, SteamFallbackBatchSize))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var outcomes = await Task.WhenAll(batch.Select(row => ResolveSteamFallbackWithOutcomeAsync(
                    row.FlingTitle,
                    row.SteamAppId,
                    cancellationToken)));
                for (var index = 0; index < batch.Count; index++)
                {
                    processed++;
                    var row = batch[index];
                    var outcome = outcomes[index];
                    if (outcome.IsError)
                    {
                        errors++;
                        consecutiveErrors++;
                        row.LastValidatedUtc = now;
                        row.LastSyncedUtc = now;
                        pendingSaves++;
                        if (consecutiveErrors >= SteamBackfillStopConsecutiveErrors)
                        {
                            stoppedByErrors = true;
                            break;
                        }
                        continue;
                    }

                    consecutiveErrors = 0;
                    if (outcome.Resolution?.MatchStatus == TrainerTitleIndexEntry.MatchStatusMatched)
                    {
                        ApplyResolution(row, outcome.Resolution, now);
                        matched++;
                    }
                    else
                    {
                        row.LastValidatedUtc = now;
                        row.LastSyncedUtc = now;
                    }
                    pendingSaves++;
                }

                if (pendingSaves >= SaveInterval || stoppedByErrors)
                {
                    await db.SaveChangesAsync(cancellationToken);
                    pendingSaves = 0;
                }

                if (stoppedByErrors)
                {
                    break;
                }
            }

            if (pendingSaves > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
            }

            if (matched > 0 || processed > 0)
            {
                _ = await _trainerTitleSnapshotService.SaveSnapshotFromDatabaseAsync(db, cancellationToken);
            }

            Logger.Info("Steam backfill completed. Processed: {Processed}, Matched: {Matched}, Errors: {Errors}, ConsecutiveErrors: {Consecutive}, StoppedByErrors: {Stopped}.",
                processed, matched, errors, consecutiveErrors, stoppedByErrors);
            return new SteamBackfillReport(processed, matched, errors, consecutiveErrors, stoppedByErrors);
        }
        finally
        {
            _syncGate.Release();
        }
    }

    private static bool ShouldRefreshMetadata(TrainerTitleIndexEntry row, bool force)
    {
        if (force || row.LastValidatedUtc == null)
        {
            return true;
        }

        if (row.MatchStatus == TrainerTitleIndexEntry.MatchStatusMatched)
        {
            if (string.IsNullOrWhiteSpace(row.EnglishName) || string.IsNullOrWhiteSpace(row.ChineseName))
            {
                return true;
            }

            return row.LastValidatedUtc.Value < DateTime.UtcNow.Subtract(MatchedRefreshInterval);
        }

        return row.LastValidatedUtc.Value < DateTime.UtcNow.Subtract(UnmatchedRefreshInterval);
    }

    private async Task<ResolvedMetadata?> ResolveGamerskyMetadataAsync(
        string flingTitle,
        CancellationToken cancellationToken)
    {
        try
        {
            var searchKeyword = TitleSearchNormalizer.RemoveTrainerSuffix(flingTitle);
            var candidates = await _gameTitleMetadataService.SearchCandidatesAsync(searchKeyword, cancellationToken);
            if (candidates.Count > 0)
            {
                var metadataEntries = await LoadGamerskyMetadataBatchAsync(candidates, cancellationToken);
                var matchedMetadata = TrainerTitleMatchResolver.TrySelectBestMetadata(flingTitle, metadataEntries);
                if (matchedMetadata != null)
                {
                    var metadata = matchedMetadata.Candidate;
                    return CreateMatchedResolution(
                        metadata.EnglishName,
                        metadata.ChineseName,
                        null,
                        metadata.Source,
                        metadata.SourceUrl,
                        matchedMetadata.Confidence);
                }
            }

            return CreateUnmatchedResolution();
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to resolve Gamersky metadata for {FlingTitle}.", flingTitle);
            return null;
        }
    }

    private async Task<ResolvedMetadata?> ResolveSteamFallbackAsync(
        string flingTitle,
        string? localAppId,
        CancellationToken cancellationToken)
    {
        var outcome = await ResolveSteamFallbackWithOutcomeAsync(flingTitle, localAppId, cancellationToken);
        return outcome.IsError ? null : outcome.Resolution;
    }

    private async Task<SteamResolutionOutcome> ResolveSteamFallbackWithOutcomeAsync(
        string flingTitle,
        string? localAppId,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(localAppId))
            {
                var directMetadata = await _steamStoreMetadataService.GetAppMetadataAsync(localAppId, cancellationToken);
                if (directMetadata != null && !string.IsNullOrWhiteSpace(directMetadata.EnglishName))
                {
                    return new SteamResolutionOutcome(CreateMatchedResolution(
                        directMetadata.EnglishName,
                        directMetadata.ChineseName,
                        directMetadata.AppId,
                        TrainerTitleIndexEntry.MetadataSourceSteam,
                        BuildSteamAppUrl(directMetadata.AppId),
                        1d), false);
                }
            }

            var searchKeyword = TitleSearchNormalizer.RemoveTrainerSuffix(flingTitle);
            var steamCandidates = await _steamStoreMetadataService.SearchAppsAsync(searchKeyword, cancellationToken);
            var matchedSteamCandidate = TrainerTitleMatchResolver.TrySelectBestSteamCandidate(flingTitle, steamCandidates);
            if (matchedSteamCandidate == null)
            {
                return new SteamResolutionOutcome(CreateUnmatchedResolution(), false);
            }

            var steamMetadata = await _steamStoreMetadataService.GetAppMetadataAsync(matchedSteamCandidate.Candidate.AppId, cancellationToken);
            if (steamMetadata == null || string.IsNullOrWhiteSpace(steamMetadata.EnglishName))
            {
                return new SteamResolutionOutcome(CreateUnmatchedResolution(), false);
            }

            return new SteamResolutionOutcome(CreateMatchedResolution(
                steamMetadata.EnglishName,
                steamMetadata.ChineseName,
                steamMetadata.AppId,
                TrainerTitleIndexEntry.MetadataSourceSteam,
                BuildSteamAppUrl(steamMetadata.AppId),
                matchedSteamCandidate.Confidence), false);
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to resolve Steam fallback metadata for {FlingTitle}.", flingTitle);
            return new SteamResolutionOutcome(null, true);
        }
    }

    private async Task<IReadOnlyList<GameTitleMetadata>> LoadGamerskyMetadataBatchAsync(
        IReadOnlyList<GameTitleSearchCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var tasks = candidates
            .Take(GamerskyDetailBatchSize)
            .Select(candidate => _gameTitleMetadataService.GetMetadataAsync(candidate.SourceKey, cancellationToken))
            .ToArray();

        var metadata = await Task.WhenAll(tasks);
        return metadata
            .Where(item => item != null && !string.IsNullOrWhiteSpace(item.EnglishName))
            .Cast<GameTitleMetadata>()
            .ToList();
    }

    private static IEnumerable<List<TrainerTitleIndexEntry>> ChunkRows(
        IReadOnlyList<TrainerTitleIndexEntry> rows,
        int chunkSize)
    {
        for (var index = 0; index < rows.Count; index += chunkSize)
        {
            yield return rows.Skip(index).Take(chunkSize).ToList();
        }
    }

    private static string? TryGetLocalAppId(
        TrainerTitleIndexEntry row,
        IReadOnlyDictionary<string, string> appIdByNormalizedTitle)
    {
        return appIdByNormalizedTitle.TryGetValue(row.NormalizedFlingTitle, out var appId)
            ? appId
            : null;
    }

    private static ResolvedMetadata CreateMatchedResolution(
        string? englishName,
        string? chineseName,
        string? steamAppId,
        string? metadataSource,
        string? metadataSourceUrl,
        double confidence)
    {
        return new ResolvedMetadata(
            TrainerTitleIndexEntry.MatchStatusMatched,
            steamAppId,
            metadataSource,
            metadataSourceUrl,
            englishName,
            chineseName,
            confidence);
    }

    private static ResolvedMetadata CreateUnmatchedResolution()
    {
        return new ResolvedMetadata(
            TrainerTitleIndexEntry.MatchStatusUnmatched,
            null,
            null,
            null,
            null,
            null,
            null);
    }

    private static void ApplyResolution(TrainerTitleIndexEntry row, ResolvedMetadata resolution, DateTime nowUtc)
    {
        row.SteamAppId = resolution.SteamAppId;
        row.MetadataSource = resolution.MetadataSource;
        row.MetadataSourceUrl = resolution.MetadataSourceUrl;
        row.EnglishName = resolution.EnglishName;
        row.ChineseName = resolution.ChineseName;
        row.NormalizedEnglishName = TitleSearchNormalizer.NormalizeEnglishTitle(resolution.EnglishName);
        row.NormalizedChineseName = TitleSearchNormalizer.NormalizeChineseTitle(resolution.ChineseName);
        row.MatchStatus = resolution.MatchStatus;
        row.MatchConfidence = resolution.MatchConfidence;
        row.LastValidatedUtc = nowUtc;
        row.LastSyncedUtc = nowUtc;
    }

    private static string BuildSteamAppUrl(string appId)
    {
        return $"https://store.steampowered.com/app/{Uri.EscapeDataString(appId)}/";
    }

    private sealed record ResolvedMetadata(
        string MatchStatus,
        string? SteamAppId,
        string? MetadataSource,
        string? MetadataSourceUrl,
        string? EnglishName,
        string? ChineseName,
        double? MatchConfidence);

    private sealed record SteamResolutionOutcome(
        ResolvedMetadata? Resolution,
        bool IsError);
}
