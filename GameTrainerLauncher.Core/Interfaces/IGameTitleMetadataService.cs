using GameTrainerLauncher.Core.Models;

namespace GameTrainerLauncher.Core.Interfaces;

public interface IGameTitleMetadataService
{
    Task<IReadOnlyList<GameTitleSearchCandidate>> SearchCandidatesAsync(string query, CancellationToken cancellationToken = default);
    Task<GameTitleMetadata?> GetMetadataAsync(string sourceKeyOrUrl, CancellationToken cancellationToken = default);
}
