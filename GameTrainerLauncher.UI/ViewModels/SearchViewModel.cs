using System.Collections.ObjectModel;
using System.Linq;
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

public partial class SearchViewModel : PageFeedbackViewModelBase
{
    private readonly IScraperService _scraperService;
    private readonly AppDbContext _dbContext;
    private readonly ITrainerLibraryService _trainerLibraryService;
    private readonly ITrainerVersionSelectionService _trainerVersionSelectionService;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAppNotificationService _notificationService;

    [ObservableProperty]
    private ObservableCollection<Trainer> _searchResults = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    [ObservableProperty]
    private bool _hasNoResults;

    public SearchViewModel(
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
    }

    [RelayCommand]
    public async Task RefreshAlreadyInLibraryAsync()
    {
        if (SearchResults.Count == 0)
        {
            return;
        }

        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var existingNames = (await db.Games.Select(game => game.Name).ToListAsync()).ToHashSet();
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (var trainer in SearchResults)
                {
                    trainer.IsDownloaded = existingNames.Contains(trainer.Title);
                }
            });
        }
        catch
        {
        }
    }

    [RelayCommand]
    public async Task RunSearchAsync()
    {
        var keyword = (SearchKeyword ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(keyword))
        {
            var msg = (string)Application.Current.FindResource("MsgSearchEmpty") ?? "Please enter a game name before searching.";
            var title = (string)Application.Current.FindResource("MsgSearchTitle") ?? "Search";
            _notificationService.ShowInfo(msg, title);
            return;
        }

        await SearchAsync(keyword);
    }

    public async Task SearchAsync(string keyword)
    {
        SearchKeyword = keyword;
        IsLoading = true;
        HasNoResults = false;
        ClearPageFeedback();
        SearchResults.Clear();

        try
        {
            var data = await _scraperService.SearchAsync(keyword);

            await _dbContext.Database.EnsureCreatedAsync();
            var existingNames = _dbContext.Games.Select(game => game.Name).ToHashSet();

            foreach (var trainer in data)
            {
                if (existingNames.Contains(trainer.Title))
                {
                    trainer.IsDownloaded = true;
                }

                SearchResults.Add(trainer);
            }

            for (var i = 0; i < SearchResults.Count; i++)
            {
                var trainer = SearchResults[i];
                if (string.IsNullOrEmpty(trainer.PageUrl))
                {
                    continue;
                }

                try
                {
                    var details = await _scraperService.GetTrainerDetailsAsync(trainer.PageUrl);
                    trainer.LastUpdated = details.LastUpdated;
                    trainer.DownloadUrl = details.DownloadUrl;
                    trainer.Version = details.Version;
                    trainer.DownloadOptions = details.DownloadOptions;
                    trainer.ImageUrl = string.IsNullOrEmpty(details.ImageUrl) ? trainer.ImageUrl : details.ImageUrl;
                }
                catch
                {
                }
            }

            if (SearchResults.Count == 0)
            {
                HasNoResults = true;
            }
        }
        catch (Exception ex)
        {
            var msg = ((string)Application.Current.FindResource("MsgSearchFailed") ?? "Search failed.") + " " + ex.Message;
            ShowPageFeedback(
                InfoBarSeverity.Error,
                (string)Application.Current.FindResource("MsgErrorTitle") ?? "Error",
                msg);
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
