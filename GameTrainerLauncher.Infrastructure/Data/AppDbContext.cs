using GameTrainerLauncher.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameTrainerLauncher.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public DbSet<Game> Games { get; set; } = null!;
    public DbSet<Trainer> Trainers { get; set; } = null!;
    public DbSet<TrainerTitleIndexEntry> TrainerTitleIndexEntries { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        AppPaths.EnsureDataFolderExists();
        var dbPath = System.IO.Path.Join(AppPaths.DataFolder, "game_trainer_launcher.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Trainer>()
            .Ignore(t => t.DownloadOptions)
            .Ignore(t => t.IsLoading)
            .Ignore(t => t.IsDownloading)
            .Ignore(t => t.DownloadProgress)
            .Ignore(t => t.IsAdding)
            .Ignore(t => t.IsAddPending)
            .Ignore(t => t.DownloadStatusText)
            .Ignore(t => t.IsDownloadProgressEstimated)
            .Ignore(t => t.DownloadStage)
            .Ignore(t => t.PrimaryDisplayTitle)
            .Ignore(t => t.SecondaryDisplayTitle)
            .Ignore(t => t.MatchedChineseName)
            .Ignore(t => t.MatchedEnglishName);

        modelBuilder.Entity<TrainerTitleIndexEntry>()
            .HasIndex(entry => entry.TrainerPageUrl)
            .IsUnique();
    }

    /// <summary>
    /// Drops columns that were previously mapped on Trainers but are now ignored (NotMapped).
    /// Run once after EnsureCreatedAsync to fix "NOT NULL constraint failed: Trainers.DownloadProgress" on existing DBs.
    /// </summary>
    public async Task MigrateTrainersTableDropIgnoredColumnsAsync(CancellationToken cancellationToken = default)
    {
        var columnsToDrop = new[]
        {
            "DownloadProgress",
            "IsDownloading",
            "IsLoading",
            "IsAdding",
            "IsAddPending",
            "DownloadStatusText",
            "IsDownloadProgressEstimated",
            "DownloadStage"
        };
        foreach (var col in columnsToDrop)
        {
            try
            {
                await Database.ExecuteSqlRawAsync(
                    $"ALTER TABLE Trainers DROP COLUMN [{col}];",
                    cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Column may not exist (new DB or already dropped); ignore.
            }
        }
    }

    /// <summary>确保 Games 表有 DisplayOrder 列（旧库升级用）。</summary>
    public async Task EnsureGamesDisplayOrderColumnAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await Database.ExecuteSqlRawAsync(
                "ALTER TABLE Games ADD COLUMN DisplayOrder INTEGER NULL;",
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Column already exists; ignore.
        }
    }

    public async Task EnsureTrainerTitleIndexSchemaAsync(CancellationToken cancellationToken = default)
    {
        await Database.ExecuteSqlRawAsync(
            """
            CREATE TABLE IF NOT EXISTS TrainerTitleIndexEntries (
                Id INTEGER NOT NULL CONSTRAINT PK_TrainerTitleIndexEntries PRIMARY KEY AUTOINCREMENT,
                FlingTitle TEXT NOT NULL,
                TrainerPageUrl TEXT NOT NULL,
                SteamAppId TEXT NULL,
                MetadataSource TEXT NULL,
                MetadataSourceUrl TEXT NULL,
                EnglishName TEXT NULL,
                ChineseName TEXT NULL,
                NormalizedFlingTitle TEXT NOT NULL,
                NormalizedEnglishName TEXT NULL,
                NormalizedChineseName TEXT NULL,
                MatchStatus TEXT NOT NULL DEFAULT 'Unmatched',
                MatchConfidence REAL NULL,
                IsActive INTEGER NOT NULL DEFAULT 1,
                LastSyncedUtc TEXT NULL,
                LastSeenUtc TEXT NULL,
                LastValidatedUtc TEXT NULL
            );
            """,
            cancellationToken).ConfigureAwait(false);

        await TryAddTrainerTitleIndexColumnAsync("MetadataSource", "TEXT NULL", cancellationToken).ConfigureAwait(false);
        await TryAddTrainerTitleIndexColumnAsync("MetadataSourceUrl", "TEXT NULL", cancellationToken).ConfigureAwait(false);
        await TryAddTrainerTitleIndexColumnAsync("MatchConfidence", "REAL NULL", cancellationToken).ConfigureAwait(false);
        await TryAddTrainerTitleIndexColumnAsync("LastValidatedUtc", "TEXT NULL", cancellationToken).ConfigureAwait(false);

        await Database.ExecuteSqlRawAsync(
            "CREATE UNIQUE INDEX IF NOT EXISTS IX_TrainerTitleIndexEntries_TrainerPageUrl ON TrainerTitleIndexEntries(TrainerPageUrl);",
            cancellationToken).ConfigureAwait(false);

        await Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_TrainerTitleIndexEntries_NormalizedChineseName ON TrainerTitleIndexEntries(NormalizedChineseName);",
            cancellationToken).ConfigureAwait(false);

        await Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_TrainerTitleIndexEntries_NormalizedEnglishName ON TrainerTitleIndexEntries(NormalizedEnglishName);",
            cancellationToken).ConfigureAwait(false);

        await Database.ExecuteSqlRawAsync(
            "CREATE INDEX IF NOT EXISTS IX_TrainerTitleIndexEntries_NormalizedFlingTitle ON TrainerTitleIndexEntries(NormalizedFlingTitle);",
            cancellationToken).ConfigureAwait(false);
    }

    private async Task TryAddTrainerTitleIndexColumnAsync(string columnName, string definition, CancellationToken cancellationToken)
    {
        try
        {
            await Database.ExecuteSqlRawAsync(
                $"ALTER TABLE TrainerTitleIndexEntries ADD COLUMN {columnName} {definition};",
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Column already exists; ignore.
        }
    }
}
