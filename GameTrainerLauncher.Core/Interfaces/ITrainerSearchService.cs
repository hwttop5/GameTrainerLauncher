using GameTrainerLauncher.Core.Models;

namespace GameTrainerLauncher.Core.Interfaces;

public interface ITrainerSearchService
{
    Task<TrainerSearchResult> SearchAsync(string keyword, CancellationToken cancellationToken = default);
}
