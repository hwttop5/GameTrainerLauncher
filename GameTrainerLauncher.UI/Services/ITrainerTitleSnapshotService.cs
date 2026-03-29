using GameTrainerLauncher.Infrastructure.Data;

namespace GameTrainerLauncher.UI.Services;

public interface ITrainerTitleSnapshotService
{
    string SnapshotPath { get; }
    string SeedSnapshotPath { get; }
    Task<int> ImportSnapshotAsync(AppDbContext db, bool overwriteExisting = false, CancellationToken cancellationToken = default);
    Task<int> ImportSeedSnapshotIfNeededAsync(AppDbContext db, CancellationToken cancellationToken = default);
    Task<bool> SaveSnapshotFromDatabaseAsync(AppDbContext db, CancellationToken cancellationToken = default);
    Task<bool> SaveSeedSnapshotFromDatabaseAsync(AppDbContext db, CancellationToken cancellationToken = default);
}
