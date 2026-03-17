using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Interfaces;
using GameTrainerLauncher.Infrastructure.Data;
using GameTrainerLauncher.UI.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
    private readonly IServiceScopeFactory _scopeFactory;

    [ObservableProperty]
    private ObservableCollection<Trainer> _searchResults = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    public SearchViewModel(IScraperService scraperService, AppDbContext dbContext, ITrainerManager trainerManager, IMyGamesRefreshService myGamesRefreshService, IServiceScopeFactory scopeFactory)
    {
        _scraperService = scraperService;
        _dbContext = dbContext;
        _trainerManager = trainerManager;
        _myGamesRefreshService = myGamesRefreshService;
        _scopeFactory = scopeFactory;
    }

    [ObservableProperty]
    private bool _hasNoResults;

    /// <summary>根据库中最新数据刷新搜索结果中每项的「已添加」状态。</summary>
    [RelayCommand]
    public async Task RefreshAlreadyInLibraryAsync()
    {
        if (SearchResults.Count == 0) return;
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var existingNames = (await db.Games.Select(g => g.Name).ToListAsync()).ToHashSet();
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                foreach (var t in SearchResults)
                    t.IsDownloaded = existingNames.Contains(t.Title);
            });
        }
        catch { /* ignore */ }
    }

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