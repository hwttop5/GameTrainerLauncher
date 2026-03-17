using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Interfaces;
using GameTrainerLauncher.Infrastructure.Data;
using GameTrainerLauncher.UI.Services;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;

namespace GameTrainerLauncher.UI.ViewModels;

public partial class SearchViewModel : ObservableObject
{
    private readonly IScraperService _scraperService;
    private readonly AppDbContext _dbContext;
    private readonly ITrainerManager _trainerManager;
    private readonly IMyGamesRefreshService _myGamesRefreshService;

    [ObservableProperty]
    private ObservableCollection<Trainer> _searchResults = new();

    [ObservableProperty]
    private Trainer? _currentAddingTrainer;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    public SearchViewModel(IScraperService scraperService, AppDbContext dbContext, ITrainerManager trainerManager, IMyGamesRefreshService myGamesRefreshService)
    {
        _scraperService = scraperService;
        _dbContext = dbContext;
        _trainerManager = trainerManager;
        _myGamesRefreshService = myGamesRefreshService;
    }

    [ObservableProperty]
    private bool _hasNoResults;

    /// <summary>Run search using current SearchKeyword (for in-page search box).</summary>
    [RelayCommand]
    public async Task RunSearchAsync()
    {
        var keyword = (SearchKeyword ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(keyword))
        {
            var msg = (string)System.Windows.Application.Current.FindResource("MsgSearchEmpty") ?? "Please enter a game name before searching.";
            var title = (string)System.Windows.Application.Current.FindResource("MsgSearchTitle") ?? "Search";
            System.Windows.MessageBox.Show(msg, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }
        await SearchAsync(keyword);
    }

    public async Task SearchAsync(string keyword)
    {
        SearchKeyword = keyword;
        IsLoading = true;
        HasNoResults = false;
        SearchResults.Clear();
        
        try
        {
            var data = await _scraperService.SearchAsync(keyword);
            
            await _dbContext.Database.EnsureCreatedAsync();
            var existingNames = _dbContext.Games.Select(g => g.Name).ToHashSet();

            foreach (var t in data) 
            {
                if (existingNames.Contains(t.Title))
                {
                    t.IsDownloaded = true;
                }
                SearchResults.Add(t);
            }

            // Enrich with details so cards show LastUpdated (and have DownloadUrl when adding to my games)
            for (var i = 0; i < SearchResults.Count; i++)
            {
                var t = SearchResults[i];
                if (string.IsNullOrEmpty(t.PageUrl)) continue;
                try
                {
                    var details = await _scraperService.GetTrainerDetailsAsync(t.PageUrl);
                    t.LastUpdated = details.LastUpdated;
                    t.DownloadUrl = details.DownloadUrl;
                    t.ImageUrl = string.IsNullOrEmpty(details.ImageUrl) ? t.ImageUrl : details.ImageUrl;
                }
                catch { /* keep existing values */ }
            }

            if (SearchResults.Count == 0)
            {
                HasNoResults = true;
            }
        }
        catch (Exception ex)
        {
            var msg = ((string)System.Windows.Application.Current.FindResource("MsgSearchFailed") ?? "Search failed.") + " " + ex.Message;
            var title = (string)System.Windows.Application.Current.FindResource("MsgErrorTitle") ?? "Error";
            System.Windows.MessageBox.Show(msg, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task AddToMyGamesAsync(Trainer trainer)
    {
        try
        {
            await _dbContext.Database.EnsureCreatedAsync();
            if (_dbContext.Games.Any(g => g.Name == trainer.Title))
            {
                var msg = (string)System.Windows.Application.Current.FindResource("MsgAlreadyInLibrary");
                var title = (string)System.Windows.Application.Current.FindResource("MsgInfoTitle");
                System.Windows.MessageBox.Show(msg, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                return;
            }
            CurrentAddingTrainer = trainer;
            _ = RunDownloadThenAddAsync(trainer);
        }
        catch (Exception ex)
        {
            CurrentAddingTrainer = null;
            var msg = (string)System.Windows.Application.Current.FindResource("MsgAddFailed") + " " + ex.Message;
            var title = (string)System.Windows.Application.Current.FindResource("MsgErrorTitle");
            System.Windows.MessageBox.Show(msg, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private async Task RunDownloadThenAddAsync(Trainer trainer)
    {
        try
        {
            var newTrainer = new Trainer
            {
                Title = trainer.Title,
                PageUrl = trainer.PageUrl,
                DownloadUrl = trainer.DownloadUrl,
                ImageUrl = trainer.ImageUrl,
                LastUpdated = trainer.LastUpdated,
                IsDownloaded = false
            };
            if (!string.IsNullOrWhiteSpace(newTrainer.PageUrl) &&
                (string.IsNullOrWhiteSpace(newTrainer.DownloadUrl) || string.IsNullOrWhiteSpace(newTrainer.ImageUrl)))
            {
                var details = await _scraperService.GetTrainerDetailsAsync(newTrainer.PageUrl);
                if (string.IsNullOrWhiteSpace(newTrainer.DownloadUrl)) newTrainer.DownloadUrl = details.DownloadUrl;
                if (details.LastUpdated != null) newTrainer.LastUpdated = details.LastUpdated;
                if (!string.IsNullOrEmpty(details.ImageUrl)) newTrainer.ImageUrl = details.ImageUrl;
            }
            if (string.IsNullOrWhiteSpace(newTrainer.DownloadUrl))
            {
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    CurrentAddingTrainer = null;
                    System.Windows.MessageBox.Show(
                        (string)System.Windows.Application.Current.FindResource("MsgDownloadFailed"),
                        (string)System.Windows.Application.Current.FindResource("MsgErrorTitle"),
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                });
                return;
            }

            var progress = new Progress<double>(p => newTrainer.DownloadProgress = p);
            var success = await _trainerManager.DownloadTrainerAsync(newTrainer, progress);

            if (success)
            {
                newTrainer.IsDownloaded = true;
                var game = new Game
                {
                    Name = trainer.Title,
                    MatchedTrainer = newTrainer,
                    AddedDate = DateTime.Now,
                    CoverUrl = newTrainer.ImageUrl ?? trainer.ImageUrl
                };
                _dbContext.Trainers.Add(newTrainer);
                _dbContext.Games.Add(game);
                await _dbContext.SaveChangesAsync();
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                trainer.IsDownloaded = success;
                CurrentAddingTrainer = null;
                var successTitle = (string)System.Windows.Application.Current.FindResource("MsgSuccessTitle");
                var successMsg = success
                    ? (string)System.Windows.Application.Current.FindResource("MsgAddedToMyGames")
                    : (string)System.Windows.Application.Current.FindResource("MsgDownloadFailed");
                System.Windows.MessageBox.Show(successMsg, successTitle, System.Windows.MessageBoxButton.OK, success ? System.Windows.MessageBoxImage.Information : System.Windows.MessageBoxImage.Error);
                if (success) _myGamesRefreshService.RequestRefresh();
            });
        }
        catch (Exception ex)
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                CurrentAddingTrainer = null;
                var msg = (string)System.Windows.Application.Current.FindResource("MsgAddFailed") + " " + ex.Message;
                var title = (string)System.Windows.Application.Current.FindResource("MsgErrorTitle");
                System.Windows.MessageBox.Show(msg, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            });
        }
    }
}