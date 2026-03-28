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
using Wpf.Ui.Controls;

namespace GameTrainerLauncher.UI.ViewModels;

public partial class PopularGamesViewModel : PageFeedbackViewModelBase
{
    private readonly IScraperService _scraperService;
    private readonly AppDbContext _dbContext;
    private readonly ITrainerLibraryService _trainerLibraryService;
    private readonly ITrainerVersionSelectionService _trainerVersionSelectionService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAppNotificationService _notificationService;
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
        ITrainerVersionSelectionService trainerVersionSelectionService,
        IServiceScopeFactory scopeFactory,
        IAppNotificationService notificationService)
    {
        _scraperService = scraperService;
        _dbContext = dbContext;
        _trainerLibraryService = trainerLibraryService;
        _trainerVersionSelectionService = trainerVersionSelectionService;
        _scopeFactory = scopeFactory;
        _notificationService = notificationService;
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
            ClearPageFeedback();
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
        catch (Exception ex)
        {
            ShowPageFeedback(
                InfoBarSeverity.Error,
                (string)Application.Current.FindResource("MsgErrorTitle"),
                $"{(string)Application.Current.FindResource("MsgPopularLoadFailed")} {ex.Message}");
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
            ClearPageFeedback();
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
        catch (Exception ex)
        {
            ShowPageFeedback(
                InfoBarSeverity.Error,
                (string)Application.Current.FindResource("MsgErrorTitle"),
                $"{(string)Application.Current.FindResource("MsgPopularLoadFailed")} {ex.Message}");
            _currentPage = Math.Max(1, _currentPage - 1);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(AllowConcurrentExecutions = true)]
    public async Task AddToMyGamesAsync(Trainer trainer)
    {
        if (trainer.IsDownloaded || trainer.IsAddPending || trainer.IsAdding)
        {
            return;
        }

        ClearPageFeedback();
        trainer.IsAddPending = true;

        try
        {
            await _dbContext.Database.EnsureCreatedAsync();
            if (_dbContext.Games.Any(game => game.Name == trainer.Title))
            {
                trainer.IsAddPending = false;
                var msg = (string)Application.Current.FindResource("MsgAlreadyInLibrary");
                var title = (string)Application.Current.FindResource("MsgInfoTitle");
                _notificationService.ShowInfo(msg, title);
                return;
            }

            if (!await _trainerVersionSelectionService.EnsureSelectionAsync(trainer))
            {
                trainer.IsAddPending = false;
                return;
            }

            trainer.IsAddPending = false;
            trainer.IsAdding = true;
            _ = RunDownloadThenAddAsync(trainer);
        }
        catch (Exception ex)
        {
            trainer.IsAddPending = false;
            trainer.IsAdding = false;
            var msg = (string)Application.Current.FindResource("MsgAddFailed") + " " + ex.Message;
            ShowPageFeedback(
                InfoBarSeverity.Error,
                (string)Application.Current.FindResource("MsgErrorTitle"),
                msg);
        }
    }

    private async Task RunDownloadThenAddAsync(Trainer trainer)
    {
        try
        {
            trainer.IsAddPending = false;
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
            if (success)
            {
                ClearPageFeedback();
                _notificationService.ShowSuccess((string)Application.Current.FindResource("MsgAddedToMyGames"));
            }
            else
            {
                ShowPageFeedback(
                    InfoBarSeverity.Error,
                    (string)Application.Current.FindResource("MsgErrorTitle"),
                    (string)Application.Current.FindResource("MsgDownloadFailed"));
            }
        }
        catch (OperationCanceledException)
        {
            trainer.IsAddPending = false;
            ResetDownloadProgress(trainer);
            trainer.IsAdding = false;
            ShowPageFeedback(
                InfoBarSeverity.Warning,
                (string)Application.Current.FindResource("MsgWarningTitle"),
                (string)Application.Current.FindResource("MsgDownloadTimeout"));
        }
        catch (Exception ex)
        {
            trainer.IsAddPending = false;
            ResetDownloadProgress(trainer);
            trainer.IsAdding = false;
            var msg = (string)Application.Current.FindResource("MsgAddFailed") + " " + ex.Message;
            ShowPageFeedback(
                InfoBarSeverity.Error,
                (string)Application.Current.FindResource("MsgErrorTitle"),
                msg);
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
