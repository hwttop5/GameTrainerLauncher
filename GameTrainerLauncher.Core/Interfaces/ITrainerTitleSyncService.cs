namespace GameTrainerLauncher.Core.Interfaces;

public interface ITrainerTitleSyncService
{
    Task EnsureSynchronizedAsync(bool force = false, CancellationToken cancellationToken = default);
    Task EnsureKeywordSynchronizedAsync(string keyword, CancellationToken cancellationToken = default);
    Task<SteamBackfillReport> EnsureSteamBackfillUntilErrorAsync(int? maxRows = null, CancellationToken cancellationToken = default);
}

public sealed record SteamBackfillReport(
    int ProcessedRows,
    int MatchedRows,
    int ErrorCount,
    int ConsecutiveErrorCount,
    bool StoppedByConsecutiveErrors);
