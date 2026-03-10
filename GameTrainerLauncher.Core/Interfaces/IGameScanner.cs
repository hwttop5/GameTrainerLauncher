namespace GameTrainerLauncher.Core.Interfaces;
using GameTrainerLauncher.Core.Entities;

public interface IGameScanner
{
    Task<List<Game>> ScanAsync();
    string PlatformName { get; }
}
