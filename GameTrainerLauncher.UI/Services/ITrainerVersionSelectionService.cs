using GameTrainerLauncher.Core.Entities;

namespace GameTrainerLauncher.UI.Services;

public interface ITrainerVersionSelectionService
{
    Task<bool> EnsureSelectionAsync(Trainer trainer, CancellationToken cancellationToken = default);
}
