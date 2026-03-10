namespace GameTrainerLauncher.Core.Interfaces;
using GameTrainerLauncher.Core.Entities;

public interface IScraperService
{
    Task<List<Trainer>> SearchAsync(string keyword);
    Task<List<Trainer>> GetPopularTrainersAsync(int page = 1);
    Task<Trainer> GetTrainerDetailsAsync(string url);
}
