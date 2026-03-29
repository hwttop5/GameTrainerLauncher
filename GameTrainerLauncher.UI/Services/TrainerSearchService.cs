using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Interfaces;
using GameTrainerLauncher.Core.Models;
using GameTrainerLauncher.Core.Utilities;
using GameTrainerLauncher.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NLog;

namespace GameTrainerLauncher.UI.Services;

public class TrainerSearchService : ITrainerSearchService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly TimeSpan SearchSyncThreshold = TimeSpan.FromHours(24);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IScraperService _scraperService;
    private readonly ITrainerTitleSyncService _trainerTitleSyncService;
    private readonly IAppSettingsService _appSettingsService;
    private readonly object _backgroundSyncLock = new();
    private readonly object _keywordSyncLock = new();
    private Task? _backgroundSyncTask;
    private readonly HashSet<string> _pendingKeywordSync = new(StringComparer.Ordinal);

    public TrainerSearchService(
        IServiceScopeFactory scopeFactory,
        IScraperService scraperService,
        ITrainerTitleSyncService trainerTitleSyncService,
        IAppSettingsService appSettingsService)
    {
        _scopeFactory = scopeFactory;
        _scraperService = scraperService;
        _trainerTitleSyncService = trainerTitleSyncService;
        _appSettingsService = appSettingsService;
    }

    public async Task<TrainerSearchResult> SearchAsync(string keyword, CancellationToken cancellationToken = default)
    {
        var trimmedKeyword = keyword?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(trimmedKeyword))
        {
            return new TrainerSearchResult();
        }

        var containsCjk = TitleSearchNormalizer.ContainsCjk(trimmedKeyword);
        var indexState = containsCjk
            ? await GetChineseIndexStateAsync(cancellationToken)
            : IndexState.Ready;

        if (containsCjk && indexState.ShouldQueueSync)
        {
            QueueBackgroundSynchronization(force: indexState.ActiveRowCount == 0);
        }

        var indexedResults = await SearchIndexedTitlesAsync(trimmedKeyword, containsCjk, cancellationToken);
        if (containsCjk)
        {
            if (indexedResults.Count == 0 && indexState.IsPotentiallyIncomplete)
            {
                QueueKeywordSynchronization(trimmedKeyword: trimmedKeyword);
            }

            return new TrainerSearchResult
            {
                Trainers = indexedResults,
                IndexMayBeIncomplete = indexState.IsPotentiallyIncomplete,
                IsIndexUpdating = indexState.ShouldQueueSync
            };
        }

        if (indexedResults.Count > 0)
        {
            return new TrainerSearchResult
            {
                Trainers = indexedResults
            };
        }

        return new TrainerSearchResult
        {
            Trainers = await _scraperService.SearchAsync(trimmedKeyword)
        };
    }

    private async Task<IndexState> GetChineseIndexStateAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync(cancellationToken);
        await db.EnsureTrainerTitleIndexSchemaAsync(cancellationToken);

        var activeRowCount = await db.TrainerTitleIndexEntries.CountAsync(row => row.IsActive, cancellationToken);
        var settings = _appSettingsService.GetSettings();
        var needsSync = activeRowCount == 0 ||
                        !settings.LastTrainerTitleSyncUtc.HasValue ||
                        DateTimeOffset.UtcNow - settings.LastTrainerTitleSyncUtc.Value >= SearchSyncThreshold;

        return new IndexState(
            activeRowCount,
            needsSync,
            activeRowCount == 0 || needsSync);
    }

    private async Task<List<Trainer>> SearchIndexedTitlesAsync(string keyword, bool containsCjk, CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync(cancellationToken);
        await db.EnsureTrainerTitleIndexSchemaAsync(cancellationToken);

        var query = db.TrainerTitleIndexEntries
            .AsNoTracking()
            .Where(row => row.IsActive);

        List<TrainerTitleIndexEntry> rows;
        if (containsCjk)
        {
            var normalizedKeyword = TitleSearchNormalizer.NormalizeChineseTitle(keyword);
            if (string.IsNullOrWhiteSpace(normalizedKeyword))
            {
                return [];
            }

            rows = await query
                .Where(row => row.MatchStatus == TrainerTitleIndexEntry.MatchStatusMatched &&
                              row.NormalizedChineseName != null &&
                              row.NormalizedChineseName.Contains(normalizedKeyword))
                .ToListAsync(cancellationToken);

            return rows
                .OrderBy(row => GetChineseRank(row, normalizedKeyword))
                .ThenBy(row => row.FlingTitle, StringComparer.OrdinalIgnoreCase)
                .Select(row => ToTrainer(row, preferChinese: true))
                .GroupBy(trainer => trainer.PageUrl, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        var normalizedEnglishKeyword = TitleSearchNormalizer.NormalizeEnglishTitle(keyword);
        if (string.IsNullOrWhiteSpace(normalizedEnglishKeyword))
        {
            return [];
        }

        rows = await query
            .Where(row => row.NormalizedFlingTitle.Contains(normalizedEnglishKeyword) ||
                          (row.NormalizedEnglishName != null && row.NormalizedEnglishName.Contains(normalizedEnglishKeyword)))
            .ToListAsync(cancellationToken);

        return rows
            .OrderBy(row => GetEnglishRank(row, normalizedEnglishKeyword))
            .ThenBy(row => row.FlingTitle, StringComparer.OrdinalIgnoreCase)
            .Select(row => ToTrainer(row, preferChinese: false))
            .GroupBy(trainer => trainer.PageUrl, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();
    }

    private void QueueBackgroundSynchronization(bool force)
    {
        lock (_backgroundSyncLock)
        {
            if (_backgroundSyncTask is { IsCompleted: false })
            {
                return;
            }

            _backgroundSyncTask = Task.Run(async () =>
            {
                try
                {
                    await _trainerTitleSyncService.EnsureSynchronizedAsync(force, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Logger.Warn(ex, "Background trainer title synchronization failed.");
                }
            });
        }
    }

    private void QueueKeywordSynchronization(string trimmedKeyword)
    {
        if (string.IsNullOrWhiteSpace(trimmedKeyword))
        {
            return;
        }

        lock (_keywordSyncLock)
        {
            if (!_pendingKeywordSync.Add(trimmedKeyword))
            {
                return;
            }
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await _trainerTitleSyncService.EnsureKeywordSynchronizedAsync(trimmedKeyword, CancellationToken.None);
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, "Keyword trainer title synchronization failed for {Keyword}.", trimmedKeyword);
            }
            finally
            {
                lock (_keywordSyncLock)
                {
                    _pendingKeywordSync.Remove(trimmedKeyword);
                }
            }
        });
    }

    private static Trainer ToTrainer(TrainerTitleIndexEntry row, bool preferChinese)
    {
        var trainer = new Trainer
        {
            Title = row.FlingTitle,
            PrimaryDisplayTitle = row.FlingTitle,
            PageUrl = row.TrainerPageUrl,
            IsDownloaded = false
        };

        var englishName = row.EnglishName;
        var chineseName = row.ChineseName;

        trainer.MatchedEnglishName = englishName;
        trainer.MatchedChineseName = chineseName;

        if (preferChinese && !string.IsNullOrWhiteSpace(chineseName))
        {
            trainer.PrimaryDisplayTitle = chineseName;
            trainer.SecondaryDisplayTitle = string.IsNullOrWhiteSpace(englishName) ||
                                            string.Equals(chineseName, englishName, StringComparison.OrdinalIgnoreCase)
                ? null
                : englishName;
            return trainer;
        }

        if (!string.IsNullOrWhiteSpace(englishName))
        {
            trainer.PrimaryDisplayTitle = englishName;
            trainer.SecondaryDisplayTitle = string.Equals(englishName, chineseName, StringComparison.OrdinalIgnoreCase)
                ? null
                : chineseName;
            return trainer;
        }

        if (!string.IsNullOrWhiteSpace(chineseName))
        {
            trainer.PrimaryDisplayTitle = chineseName;
            trainer.SecondaryDisplayTitle = null;
        }

        return trainer;
    }

    private static int GetChineseRank(TrainerTitleIndexEntry row, string normalizedKeyword)
    {
        if (row.NormalizedChineseName == normalizedKeyword)
        {
            return 0;
        }

        if (!string.IsNullOrWhiteSpace(row.NormalizedChineseName) &&
            row.NormalizedChineseName.StartsWith(normalizedKeyword, StringComparison.Ordinal))
        {
            return 1;
        }

        return 2;
    }

    private static int GetEnglishRank(TrainerTitleIndexEntry row, string normalizedKeyword)
    {
        if (row.NormalizedEnglishName == normalizedKeyword || row.NormalizedFlingTitle == normalizedKeyword)
        {
            return 0;
        }

        if ((!string.IsNullOrWhiteSpace(row.NormalizedEnglishName) &&
             row.NormalizedEnglishName.StartsWith(normalizedKeyword, StringComparison.Ordinal)) ||
            row.NormalizedFlingTitle.StartsWith(normalizedKeyword, StringComparison.Ordinal))
        {
            return 1;
        }

        return 2;
    }

    private sealed record IndexState(int ActiveRowCount, bool ShouldQueueSync, bool IsPotentiallyIncomplete)
    {
        public static IndexState Ready { get; } = new(0, false, false);
    }
}
