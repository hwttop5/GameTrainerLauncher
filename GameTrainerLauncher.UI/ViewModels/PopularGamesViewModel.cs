using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Interfaces;
using GameTrainerLauncher.Core.Models;
using GameTrainerLauncher.Infrastructure.Data;
using GameTrainerLauncher.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GameTrainerLauncher.UI.ViewModels;

public partial class PopularGamesViewModel : ObservableObject
{
    private readonly IScraperService _scraperService;
    private readonly AppDbContext _dbContext;
    private readonly ITrainerLibraryService _trainerLibraryService;
    private readonly IServiceScopeFactory _scopeFactory;
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

    public PopularGamesViewModel(
        IScraperService scraperService,
        AppDbContext dbContext,
        ITrainerLibraryService trainerLibraryService,
        IServiceScopeFactory scopeFactory)
    {
        _scraperService = scraperService;
        _dbContext = dbContext;
        _trainerLibraryService = trainerLibraryService;
        _scopeFactory = scopeFactory;
        LoadDataCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    public async Task RefreshAlreadyInLibraryAsync()
    {
        if (Trainers.Count == 0)
        {
            return;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var existingNames = await db.Games.Select(game => game.Name).ToListAsync();
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (var trainer in Trainers)
                {
                    trainer.IsDownloaded = existingNames.Any(name =>
                        string.Equals(name.Trim(), trainer.Title.Trim(), StringComparison.OrdinalIgnoreCase));
                }
            });
        }
        catch
        {
        }
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        if (IsLoading)
        {
            return;
        }

        IsLoading = true;
        try
        {
            _currentPage = 1;
            var data = await _scraperService.GetPopularTrainersAsync(_currentPage);
            Trainers.Clear();

            await _dbContext.Database.EnsureCreatedAsync();
            var existingNames = _dbContext.Games.Select(game => game.Name).ToList();

            foreach (var trainer in data)
            {
                var isAdded = existingNames.Any(name =>
                    string.Equals(name.Trim(), trainer.Title.Trim(), StringComparison.OrdinalIgnoreCase));
                if (isAdded)
                {
                    trainer.IsDownloaded = true;
                }

                Trainers.Add(trainer);
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
        if (IsLoading || !CanLoadMore)
        {
            return;
        }

        IsLoading = true;
        try
        {
            _currentPage++;
            var data = await _scraperService.GetPopularTrainersAsync(_currentPage);

            if (data.Count == 0)
            {
                CanLoadMore = false;
                return;
            }

            await _dbContext.Database.EnsureCreatedAsync();
            var existingNames = _dbContext.Games.Select(game => game.Name).ToList();
            foreach (var trainer in data)
            {
                var isAdded = existingNames.Any(name =>
                    string.Equals(name.Trim(), trainer.Title.Trim(), StringComparison.OrdinalIgnoreCase));
                if (isAdded)
                {
                    trainer.IsDownloaded = true;
                }

                Trainers.Add(trainer);
            }
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
            if (_dbContext.Games.Any(game => game.Name == trainer.Title))
            {
                var msg = (string)Application.Current.FindResource("MsgAlreadyInLibrary");
                var title = (string)Application.Current.FindResource("MsgInfoTitle");
                MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            trainer.IsAdding = true;
            _ = RunDownloadThenAddAsync(trainer);
        }
        catch (Exception ex)
        {
            trainer.IsAdding = false;
            var msg = (string)Application.Current.FindResource("MsgAddFailed") + " " + ex.Message;
            var title = (string)Application.Current.FindResource("MsgErrorTitle");
            MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task RunDownloadThenAddAsync(Trainer trainer)
    {
        try
        {
            ResetDownloadProgress(trainer);
            trainer.DownloadStatusText = "Preparing download...";
            trainer.IsDownloadProgressEstimated = true;
            trainer.DownloadStage = TrainerDownloadStage.Preparing;

            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
            var progress = new Progress<TrainerDownloadProgress>(update => ApplyDownloadProgress(trainer, update));
            var success = await _trainerLibraryService.DownloadAndAddToLibraryAsync(trainer, progress, timeoutCts.Token);

            if (success)
            {
                trainer.DownloadProgress = 100;
                trainer.DownloadStatusText = "Completed.";
                trainer.IsDownloadProgressEstimated = false;
                trainer.DownloadStage = TrainerDownloadStage.Finalizing;
                await Task.Delay(250);
            }

            trainer.IsDownloaded = success;
            ResetDownloadProgress(trainer);
            trainer.IsAdding = false;

            var titleKey = success ? "MsgSuccessTitle" : "MsgErrorTitle";
            var messageKey = success ? "MsgAddedToMyGames" : "MsgDownloadFailed";
            MessageBox.Show(
                (string)Application.Current.FindResource(messageKey),
                (string)Application.Current.FindResource(titleKey),
                MessageBoxButton.OK,
                success ? MessageBoxImage.Information : MessageBoxImage.Error);
        }
        catch (OperationCanceledException)
        {
            ResetDownloadProgress(trainer);
            trainer.IsAdding = false;
            MessageBox.Show(
                (string)Application.Current.FindResource("MsgDownloadTimeout"),
                (string)Application.Current.FindResource("MsgErrorTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            ResetDownloadProgress(trainer);
            trainer.IsAdding = false;
            var msg = (string)Application.Current.FindResource("MsgAddFailed") + " " + ex.Message;
            var title = (string)Application.Current.FindResource("MsgErrorTitle");
            MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private static void ApplyDownloadProgress(Trainer trainer, TrainerDownloadProgress update)
    {
        trainer.DownloadProgress = update.Percent;
        trainer.DownloadStatusText = update.StatusText;
        trainer.IsDownloadProgressEstimated = update.IsEstimated;
        trainer.DownloadStage = update.Stage;
    }

    private static void ResetDownloadProgress(Trainer trainer)
    {
        trainer.DownloadProgress = 0;
        trainer.DownloadStatusText = null;
        trainer.IsDownloadProgressEstimated = false;
        trainer.DownloadStage = TrainerDownloadStage.Preparing;
    }
}
