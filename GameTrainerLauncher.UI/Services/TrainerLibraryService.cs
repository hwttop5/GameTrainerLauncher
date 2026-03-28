using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Interfaces;
using GameTrainerLauncher.Core.Models;
using GameTrainerLauncher.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameTrainerLauncher.UI.Services;

public class TrainerLibraryService : ITrainerLibraryService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IScraperService _scraperService;
    private readonly ITrainerManager _trainerManager;
    private readonly IGameCoverService _coverService;
    private readonly IMyGamesRefreshService _myGamesRefreshService;

    public TrainerLibraryService(
        IServiceScopeFactory scopeFactory,
        IScraperService scraperService,
        ITrainerManager trainerManager,
        IGameCoverService coverService,
        IMyGamesRefreshService myGamesRefreshService)
    {
        _scopeFactory = scopeFactory;
        _scraperService = scraperService;
        _trainerManager = trainerManager;
        _coverService = coverService;
        _myGamesRefreshService = myGamesRefreshService;
    }

    public async Task<bool> DownloadAndAddToLibraryAsync(
        Trainer sourceTrainer,
        IProgress<TrainerDownloadProgress> progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sourceTrainer);
        ArgumentNullException.ThrowIfNull(progress);

        var newTrainer = new Trainer
        {
            Title = sourceTrainer.Title,
            PageUrl = sourceTrainer.PageUrl,
            DownloadUrl = sourceTrainer.DownloadUrl,
            ImageUrl = sourceTrainer.ImageUrl,
            LastUpdated = sourceTrainer.LastUpdated,
            IsDownloaded = false
        };

        if (!string.IsNullOrWhiteSpace(newTrainer.PageUrl) &&
            (string.IsNullOrWhiteSpace(newTrainer.DownloadUrl) || string.IsNullOrWhiteSpace(newTrainer.ImageUrl)))
        {
            var details = await _scraperService.GetTrainerDetailsAsync(newTrainer.PageUrl);
            if (string.IsNullOrWhiteSpace(newTrainer.DownloadUrl))
            {
                newTrainer.DownloadUrl = details.DownloadUrl;
            }

            if (details.LastUpdated != null)
            {
                newTrainer.LastUpdated = details.LastUpdated;
            }

            if (!string.IsNullOrEmpty(details.ImageUrl))
            {
                newTrainer.ImageUrl = details.ImageUrl;
            }
        }

        if (string.IsNullOrWhiteSpace(newTrainer.DownloadUrl))
        {
            return false;
        }

        var success = await _trainerManager.DownloadTrainerAsync(newTrainer, progress, cancellationToken);
        if (!success)
        {
            return false;
        }

        newTrainer.IsDownloaded = true;

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync(cancellationToken);

        if (await db.Games.AnyAsync(game => game.Name == sourceTrainer.Title, cancellationToken))
        {
            _myGamesRefreshService.RequestRefresh();
            return true;
        }

        var game = new Game
        {
            Name = sourceTrainer.Title,
            MatchedTrainer = newTrainer,
            AddedDate = DateTime.Now,
            CoverUrl = newTrainer.ImageUrl ?? sourceTrainer.ImageUrl
        };

        db.Trainers.Add(newTrainer);
        db.Games.Add(game);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var coverUrl = newTrainer.ImageUrl ?? sourceTrainer.ImageUrl ?? game.CoverUrl;
            _ = _coverService.EnsureCoverAsync(game.Id, coverUrl);
        }
        catch
        {
        }

        _myGamesRefreshService.RequestRefresh();
        return true;
    }
}
