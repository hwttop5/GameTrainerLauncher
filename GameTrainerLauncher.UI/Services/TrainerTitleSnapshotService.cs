using System.Text.Json;
using System.IO;
using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Infrastructure;
using GameTrainerLauncher.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NLog;

namespace GameTrainerLauncher.UI.Services;

public sealed class TrainerTitleSnapshotService : ITrainerTitleSnapshotService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private const int SnapshotSchemaVersion = 1;
    private const string SnapshotFileName = "title-index.snapshot.json";
    private const string SeedSnapshotFileName = "title-index.seed.snapshot.json";

    public string SnapshotPath => Path.Combine(AppPaths.DataFolder, SnapshotFileName);
    public string SeedSnapshotPath
    {
        get
        {
            var repoAssetsPath = Path.Combine(Directory.GetCurrentDirectory(), "GameTrainerLauncher.UI", "Assets", SeedSnapshotFileName);
            if (Directory.Exists(Path.GetDirectoryName(repoAssetsPath)))
            {
                return repoAssetsPath;
            }

            var assetsPath = Path.Combine(AppContext.BaseDirectory, "Assets", SeedSnapshotFileName);
            if (File.Exists(assetsPath))
            {
                return assetsPath;
            }

            return Path.Combine(AppContext.BaseDirectory, SeedSnapshotFileName);
        }
    }

    public async Task<int> ImportSnapshotAsync(AppDbContext db, bool overwriteExisting = false, CancellationToken cancellationToken = default)
    {
        try
        {
            var snapshot = await LoadSnapshotAsync(SnapshotPath, cancellationToken);
            if (snapshot == null || snapshot.Items.Count == 0)
            {
                return 0;
            }

            await db.Database.EnsureCreatedAsync(cancellationToken);
            await db.EnsureTrainerTitleIndexSchemaAsync(cancellationToken);

            var existingRows = await db.TrainerTitleIndexEntries.ToListAsync(cancellationToken);
            var byPageUrl = existingRows.ToDictionary(row => row.TrainerPageUrl, StringComparer.OrdinalIgnoreCase);
            var changedCount = 0;

            foreach (var item in snapshot.Items)
            {
                if (string.IsNullOrWhiteSpace(item.TrainerPageUrl) || string.IsNullOrWhiteSpace(item.FlingTitle))
                {
                    continue;
                }

                if (!byPageUrl.TryGetValue(item.TrainerPageUrl, out var row))
                {
                    row = new TrainerTitleIndexEntry
                    {
                        FlingTitle = item.FlingTitle,
                        TrainerPageUrl = item.TrainerPageUrl,
                        NormalizedFlingTitle = item.NormalizedFlingTitle ?? item.FlingTitle,
                        IsActive = item.IsActive
                    };
                    db.TrainerTitleIndexEntries.Add(row);
                    byPageUrl[item.TrainerPageUrl] = row;
                    changedCount++;
                }
                else if (!overwriteExisting &&
                         row.LastValidatedUtc.HasValue &&
                         item.LastValidatedUtc.HasValue &&
                         row.LastValidatedUtc.Value > item.LastValidatedUtc.Value)
                {
                    continue;
                }

                row.FlingTitle = item.FlingTitle;
                row.SteamAppId = item.SteamAppId;
                row.MetadataSource = item.MetadataSource;
                row.MetadataSourceUrl = item.MetadataSourceUrl;
                row.EnglishName = item.EnglishName;
                row.ChineseName = item.ChineseName;
                row.NormalizedFlingTitle = item.NormalizedFlingTitle ?? row.NormalizedFlingTitle;
                row.NormalizedEnglishName = item.NormalizedEnglishName;
                row.NormalizedChineseName = item.NormalizedChineseName;
                row.MatchStatus = string.IsNullOrWhiteSpace(item.MatchStatus)
                    ? TrainerTitleIndexEntry.MatchStatusUnmatched
                    : item.MatchStatus;
                row.MatchConfidence = item.MatchConfidence;
                row.IsActive = item.IsActive;
                row.LastSyncedUtc = item.LastSyncedUtc;
                row.LastSeenUtc = item.LastSeenUtc;
                row.LastValidatedUtc = item.LastValidatedUtc;
                changedCount++;
            }

            if (changedCount > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
            }

            Logger.Info("Trainer title snapshot import completed from {Path}. Changed rows: {Count}.", SnapshotPath, changedCount);
            return changedCount;
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to import trainer title snapshot.");
            return 0;
        }
    }

    public async Task<bool> SaveSnapshotFromDatabaseAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        return await SaveSnapshotToPathAsync(db, SnapshotPath, cancellationToken);
    }

    public async Task<bool> SaveSeedSnapshotFromDatabaseAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        return await SaveSnapshotToPathAsync(db, SeedSnapshotPath, cancellationToken);
    }

    public async Task<int> ImportSeedSnapshotIfNeededAsync(AppDbContext db, CancellationToken cancellationToken = default)
    {
        try
        {
            var seedSnapshot = await LoadSnapshotAsync(SeedSnapshotPath, cancellationToken);
            if (seedSnapshot == null || seedSnapshot.Items.Count == 0)
            {
                Logger.Info("Seed snapshot not found or empty: {SeedPath}", SeedSnapshotPath);
                return 0;
            }

            var localSnapshot = await LoadSnapshotAsync(SnapshotPath, cancellationToken);
            var shouldReplaceLocal = localSnapshot == null ||
                                     CompareSnapshotFreshness(seedSnapshot, localSnapshot) > 0;
            if (!shouldReplaceLocal)
            {
                return 0;
            }

            AppPaths.EnsureDataFolderExists();
            var tempPath = SnapshotPath + ".tmp";
            var json = JsonSerializer.Serialize(seedSnapshot, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(tempPath, json, cancellationToken);
            File.Move(tempPath, SnapshotPath, overwrite: true);

            Logger.Info("Seed snapshot applied to local snapshot path. Seed: {SeedPath}, Local: {LocalPath}.", SeedSnapshotPath, SnapshotPath);
            return await ImportSnapshotAsync(db, overwriteExisting: false, cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to import seed snapshot if needed.");
            return 0;
        }
    }

    private async Task<bool> SaveSnapshotToPathAsync(AppDbContext db, string outputPath, CancellationToken cancellationToken)
    {
        try
        {
            await db.Database.EnsureCreatedAsync(cancellationToken);
            await db.EnsureTrainerTitleIndexSchemaAsync(cancellationToken);

            var rows = await db.TrainerTitleIndexEntries
                .AsNoTracking()
                .OrderBy(row => row.FlingTitle)
                .ToListAsync(cancellationToken);

            var snapshot = new TrainerTitleSnapshot
            {
                SchemaVersion = SnapshotSchemaVersion,
                CreatedUtc = DateTimeOffset.UtcNow,
                Items = rows.Select(row => new TrainerTitleSnapshotItem
                {
                    FlingTitle = row.FlingTitle,
                    TrainerPageUrl = row.TrainerPageUrl,
                    SteamAppId = row.SteamAppId,
                    MetadataSource = row.MetadataSource,
                    MetadataSourceUrl = row.MetadataSourceUrl,
                    EnglishName = row.EnglishName,
                    ChineseName = row.ChineseName,
                    NormalizedFlingTitle = row.NormalizedFlingTitle,
                    NormalizedEnglishName = row.NormalizedEnglishName,
                    NormalizedChineseName = row.NormalizedChineseName,
                    MatchStatus = row.MatchStatus,
                    MatchConfidence = row.MatchConfidence,
                    IsActive = row.IsActive,
                    LastSyncedUtc = row.LastSyncedUtc,
                    LastSeenUtc = row.LastSeenUtc,
                    LastValidatedUtc = row.LastValidatedUtc
                }).ToList()
            };

            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }
            else
            {
                AppPaths.EnsureDataFolderExists();
            }

            var tempPath = outputPath + ".tmp";
            var json = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(tempPath, json, cancellationToken);
            File.Move(tempPath, outputPath, overwrite: true);

            Logger.Info("Trainer title snapshot saved: {Path} ({Count} rows).", outputPath, snapshot.Items.Count);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to save trainer title snapshot.");
            return false;
        }
    }

    private async Task<TrainerTitleSnapshot?> LoadSnapshotAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var snapshot = JsonSerializer.Deserialize<TrainerTitleSnapshot>(json);
        if (snapshot == null)
        {
            return null;
        }

        if (snapshot.SchemaVersion <= 0)
        {
            return null;
        }

        return snapshot;
    }

    private static int CompareSnapshotFreshness(TrainerTitleSnapshot left, TrainerTitleSnapshot right)
    {
        var schemaCompare = left.SchemaVersion.CompareTo(right.SchemaVersion);
        if (schemaCompare != 0)
        {
            return schemaCompare;
        }

        return left.CreatedUtc.CompareTo(right.CreatedUtc);
    }

    private sealed class TrainerTitleSnapshot
    {
        public int SchemaVersion { get; set; } = SnapshotSchemaVersion;
        public DateTimeOffset CreatedUtc { get; set; }
        public List<TrainerTitleSnapshotItem> Items { get; set; } = [];
    }

    private sealed class TrainerTitleSnapshotItem
    {
        public string FlingTitle { get; set; } = string.Empty;
        public string TrainerPageUrl { get; set; } = string.Empty;
        public string? SteamAppId { get; set; }
        public string? MetadataSource { get; set; }
        public string? MetadataSourceUrl { get; set; }
        public string? EnglishName { get; set; }
        public string? ChineseName { get; set; }
        public string? NormalizedFlingTitle { get; set; }
        public string? NormalizedEnglishName { get; set; }
        public string? NormalizedChineseName { get; set; }
        public string? MatchStatus { get; set; }
        public double? MatchConfidence { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime? LastSyncedUtc { get; set; }
        public DateTime? LastSeenUtc { get; set; }
        public DateTime? LastValidatedUtc { get; set; }
    }
}
