namespace GameTrainerLauncher.Core.Interfaces;

public interface IGameCoverService
{
    /// <summary>Returns the expected local cover path for a library game.</summary>
    string GetCoverFilePath(int gameId, string? coverUrl = null);

    /// <summary>Checks whether a library game already has a local cover file.</summary>
    bool HasCover(int gameId);

    /// <summary>Ensures a library game cover exists locally.</summary>
    Task<bool> EnsureCoverAsync(int gameId, string? coverUrl, CancellationToken cancellationToken = default);

    /// <summary>Tries to resolve a cached transient trainer cover path.</summary>
    bool TryGetTrainerCoverPath(string? pageUrl, string? imageUrl, out string path);

    /// <summary>Ensures a transient trainer cover exists locally and returns its path.</summary>
    Task<string?> EnsureTrainerCoverAsync(string? pageUrl, string? imageUrl, CancellationToken cancellationToken = default);

    /// <summary>Deletes local cover files for a library game.</summary>
    void DeleteCover(int gameId);
}
