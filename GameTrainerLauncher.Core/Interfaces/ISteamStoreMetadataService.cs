using GameTrainerLauncher.Core.Models;

namespace GameTrainerLauncher.Core.Interfaces;

public interface ISteamStoreMetadataService
{
    Task<IReadOnlyList<SteamStoreSearchCandidate>> SearchAppsAsync(string keyword, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SteamStoreSearchCandidate>> SearchAppsAsync(string keyword, string language, CancellationToken cancellationToken = default);
    Task<SteamAppMetadata?> GetAppMetadataAsync(string appId, CancellationToken cancellationToken = default);
}
