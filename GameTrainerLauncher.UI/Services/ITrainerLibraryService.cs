using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Models;

namespace GameTrainerLauncher.UI.Services;

public interface ITrainerLibraryService
{
    Task<bool> DownloadAndAddToLibraryAsync(
        Trainer sourceTrainer,
        IProgress<TrainerDownloadProgress> progress,
        CancellationToken cancellationToken = default);
}
