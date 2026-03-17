using GameTrainerLauncher.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace GameTrainerLauncher.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public DbSet<Game> Games { get; set; } = null!;
    public DbSet<Trainer> Trainers { get; set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var appPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        
        if (!System.IO.Directory.Exists(appPath))
        {
            System.IO.Directory.CreateDirectory(appPath);
        }

        var dbPath = System.IO.Path.Join(appPath, "game_trainer_launcher.db");
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Trainer>()
            .Ignore(t => t.IsLoading)
            .Ignore(t => t.IsDownloading)
            .Ignore(t => t.DownloadProgress);
    }

    /// <summary>
    /// Drops columns that were previously mapped on Trainers but are now ignored (NotMapped).
    /// Run once after EnsureCreatedAsync to fix "NOT NULL constraint failed: Trainers.DownloadProgress" on existing DBs.
    /// </summary>
    public async Task MigrateTrainersTableDropIgnoredColumnsAsync(CancellationToken cancellationToken = default)
    {
        var columnsToDrop = new[] { "DownloadProgress", "IsDownloading", "IsLoading" };
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
}
