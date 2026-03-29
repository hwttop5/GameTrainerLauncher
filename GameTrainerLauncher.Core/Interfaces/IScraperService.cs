namespace GameTrainerLauncher.Core.Interfaces;
using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Models;

public interface IScraperService
{
    Task<List<Trainer>> SearchAsync(string keyword);
    Task<List<Trainer>> GetPopularTrainersAsync(int page = 1);
    Task<Trainer> GetTrainerDetailsAsync(string url);
    Task<List<TrainerCatalogEntry>> GetTrainerCatalogEntriesAsync();
}
