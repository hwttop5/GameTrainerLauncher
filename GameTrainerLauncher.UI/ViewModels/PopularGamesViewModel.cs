using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Interfaces;
using GameTrainerLauncher.Infrastructure.Data;
using System.Collections.ObjectModel;
using System.Windows;

namespace GameTrainerLauncher.UI.ViewModels;

public partial class PopularGamesViewModel : ObservableObject
{
    private readonly IScraperService _scraperService;
    private readonly AppDbContext _dbContext;
    private readonly ITrainerManager _trainerManager;
    private int _currentPage = 1;

    [ObservableProperty]
    private ObservableCollection<Trainer> _trainers = new();

    /// <summary>When set, only this card's Add button shows loading (by reference).</summary>
    [ObservableProperty]
    private Trainer? _currentAddingTrainer;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoadMoreVisible))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoadMoreVisible))]
    private bool _canLoadMore = true;

    public bool IsLoadMoreVisible => CanLoadMore && !IsLoading;

    public PopularGamesViewModel(IScraperService scraperService, AppDbContext dbContext, ITrainerManager trainerManager)
    {
        _scraperService = scraperService;
        _dbContext = dbContext;
        _trainerManager = trainerManager;
        LoadDataCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            // Reset page
            _currentPage = 1;
            var data = await _scraperService.GetPopularTrainersAsync(_currentPage);
            Trainers.Clear();
            
            // Get existing game names to check for duplicates/added status
            await _dbContext.Database.EnsureCreatedAsync();
            var existingNames = _dbContext.Games.Select(g => g.Name).ToHashSet();

            foreach (var t in data) 
            {
                // Simple check: if game name exists, mark as added (UI can disable button)
                // Note: Trainer entity might need an IsAdded property for UI binding
                if (existingNames.Contains(t.Title))
                {
                    t.IsDownloaded = true; // Reusing this flag for "Added" status in UI for now, or add a new property
                }
                Trainers.Add(t);
            }
            CanLoadMore = data.Count > 0;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task LoadMoreAsync()
    {
        if (IsLoading || !CanLoadMore) return;
        IsLoading = true;
        try
        {
            _currentPage++;
            var data = await _scraperService.GetPopularTrainersAsync(_currentPage);
            
            if (data.Count == 0)
            {
                CanLoadMore = false;
            }
            else
            {
                await _dbContext.Database.EnsureCreatedAsync();
                var existingNames = _dbContext.Games.Select(g => g.Name).ToHashSet();
                foreach (var t in data)
                {
                    if (existingNames.Contains(t.Title))
                    {
                        t.IsDownloaded = true; 
                    }
                    Trainers.Add(t);
                }
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    /// <summary>Like Remove: command returns after quick duplicate check, then background run; only UI trigger disables the one card.</summary>
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
            _ = RunAddAndDownloadAsync(trainer);
        }
        catch (Exception ex)
        {
            CurrentAddingTrainer = null;
            var msg = (string)System.Windows.Application.Current.FindResource("MsgAddFailed") + " " + ex.Message;
            var title = (string)System.Windows.Application.Current.FindResource("MsgErrorTitle");
            System.Windows.MessageBox.Show(msg, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    private async Task RunAddAndDownloadAsync(Trainer trainer)
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
            var game = new Game
            {
                Name = trainer.Title,
                MatchedTrainer = newTrainer,
                AddedDate = DateTime.Now,
                CoverUrl = trainer.ImageUrl
            };
            _dbContext.Games.Add(game);
            await _dbContext.SaveChangesAsync();

            var downloadOk = await DownloadAfterAddAsync(newTrainer);
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                trainer.IsDownloaded = downloadOk;
                CurrentAddingTrainer = null;
                var successTitle = (string)System.Windows.Application.Current.FindResource("MsgSuccessTitle");
                var successMsg = downloadOk
                    ? (string)System.Windows.Application.Current.FindResource("MsgAddedToMyGames")
                    : ((string)System.Windows.Application.Current.FindResource("MsgAddedToMyGames") ?? "已添加") + "，但下载失败，请检查网络后重试。";
                System.Windows.MessageBox.Show(successMsg, successTitle, System.Windows.MessageBoxButton.OK, downloadOk ? System.Windows.MessageBoxImage.Information : System.Windows.MessageBoxImage.Warning);
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

    private async Task<bool> DownloadAfterAddAsync(Trainer newTrainer)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(newTrainer.DownloadUrl) && !string.IsNullOrWhiteSpace(newTrainer.PageUrl))
            {
                var details = await _scraperService.GetTrainerDetailsAsync(newTrainer.PageUrl);
                newTrainer.DownloadUrl = details.DownloadUrl;
                newTrainer.LastUpdated = details.LastUpdated;
                if (!string.IsNullOrEmpty(details.ImageUrl)) newTrainer.ImageUrl = details.ImageUrl;
                _dbContext.Trainers.Update(newTrainer);
                await _dbContext.SaveChangesAsync();
            }
            if (string.IsNullOrWhiteSpace(newTrainer.DownloadUrl)) return false;

            var progress = new Progress<double>(p => newTrainer.DownloadProgress = p);
            var success = await _trainerManager.DownloadTrainerAsync(newTrainer, progress);
            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                if (success)
                {
                    newTrainer.IsDownloaded = true;
                    _dbContext.Trainers.Update(newTrainer);
                    await _dbContext.SaveChangesAsync();
                }
            });
            return success;
        }
        catch
        {
            return false;
        }
    }
}
