namespace GameTrainerLauncher.Core.Interfaces;
using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Models;

public interface ITrainerManager
{
    Task<bool> DownloadTrainerAsync(Trainer trainer, IProgress<TrainerDownloadProgress> progress, CancellationToken cancellationToken = default);
    Task<bool> LaunchTrainerAsync(Trainer trainer);
    Task<bool> DeleteTrainerAsync(Trainer trainer);
}
