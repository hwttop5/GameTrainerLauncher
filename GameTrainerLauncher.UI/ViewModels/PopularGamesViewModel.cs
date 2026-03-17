using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Interfaces;
using GameTrainerLauncher.Infrastructure.Data;
using GameTrainerLauncher.UI.Services;
using System.Collections.ObjectModel;
using System.Windows;

namespace GameTrainerLauncher.UI.ViewModels;

public partial class PopularGamesViewModel : ObservableObject
{
    private readonly IScraperService _scraperService;
    private readonly AppDbContext _dbContext;
    private readonly ITrainerManager _trainerManager;
    private readonly IMyGamesRefreshService _myGamesRefreshService;
    private int _currentPage = 1;

    [ObservableProperty]
    private ObservableCollection<Trainer> _trainers = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoadMoreVisible))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoadMoreVisible))]
    private bool _canLoadMore = true;

    public bool IsLoadMoreVisible => CanLoadMore && !IsLoading;

    public PopularGamesViewModel(IScraperService scraperService, AppDbContext dbContext, ITrainerManager trainerManager, IMyGamesRefreshService myGamesRefreshService)
    {
        _scraperService = scraperService;
        _dbContext = dbContext;
        _trainerManager = trainerManager;
        _myGamesRefreshService = myGamesRefreshService;
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
            trainer.IsAdding = true;
            _ = RunDownloadThenAddAsync(trainer);
        }
        catch (Exception ex)
        {
            trainer.IsAdding = false;
            var msg = (string)System.Windows.Application.Current.FindResource("MsgAddFailed") + " " + ex.Message;
            var title = (string)System.Windows.Application.Current.FindResource("MsgErrorTitle");
            System.Windows.MessageBox.Show(msg, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    /// <summary>模拟进度：每秒约 +5%，上限 90%，用于无 Content-Length 的下载。</summary>
    private async Task RunSimulatedProgressAsync(Trainer trainer, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && trainer.DownloadProgress < 90)
        {
            await Task.Delay(1000, ct);
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                trainer.DownloadProgress = Math.Min(90, trainer.DownloadProgress + 5);
            });
        }
    }

    /// <summary>Download first, then add to DB so the game only appears in My Games after download completes.</summary>
    private async Task RunDownloadThenAddAsync(Trainer trainer)
    {
        CancellationTokenSource? progressCts = null;
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
                    trainer.IsAdding = false;
                    System.Windows.MessageBox.Show(
                        (string)System.Windows.Application.Current.FindResource("MsgDownloadFailed"),
                        (string)System.Windows.Application.Current.FindResource("MsgErrorTitle"),
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                });
                return;
            }

            // 模拟进度 + 1 分钟下载超时
            trainer.DownloadProgress = 0;
            progressCts = new CancellationTokenSource();
            var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            var progressTask = RunSimulatedProgressAsync(trainer, progressCts.Token);

            var progress = new Progress<double>(p =>
            {
                Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (p > trainer.DownloadProgress) trainer.DownloadProgress = Math.Min(99, p);
                });
            });

            bool success;
            try
            {
                success = await _trainerManager.DownloadTrainerAsync(newTrainer, progress, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested)
            {
                progressCts.Cancel();
                try { await progressTask; } catch (OperationCanceledException) { }
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    trainer.IsAdding = false;
                    trainer.DownloadProgress = 0;
                    System.Windows.MessageBox.Show(
                        (string)System.Windows.Application.Current.FindResource("MsgDownloadTimeout"),
                        (string)System.Windows.Application.Current.FindResource("MsgErrorTitle"),
                        System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
                });
                return;
            }

            progressCts.Cancel();
            try { await progressTask; } catch (OperationCanceledException) { }

            await Application.Current.Dispatcher.InvokeAsync(() => trainer.DownloadProgress = 100);
            await Task.Delay(400);

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
                trainer.DownloadProgress = 0;
                trainer.IsAdding = false;
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
            progressCts?.Cancel();
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                trainer.DownloadProgress = 0;
                trainer.IsAdding = false;
                var msg = (string)System.Windows.Application.Current.FindResource("MsgAddFailed") + " " + ex.Message;
                var title = (string)System.Windows.Application.Current.FindResource("MsgErrorTitle");
                System.Windows.MessageBox.Show(msg, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            });
        }
    }
}
