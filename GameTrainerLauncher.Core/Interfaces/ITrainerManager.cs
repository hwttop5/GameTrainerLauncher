namespace GameTrainerLauncher.Core.Interfaces;
using GameTrainerLauncher.Core.Entities;

public interface ITrainerManager
{
    Task<bool> DownloadTrainerAsync(Trainer trainer, IProgress<double> progress, CancellationToken cancellationToken = default);
    Task<bool> LaunchTrainerAsync(Trainer trainer);
    Task<bool> DeleteTrainerAsync(Trainer trainer);
}
