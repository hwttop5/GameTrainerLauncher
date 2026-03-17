using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.UI;
using GameTrainerLauncher.UI.Services;
using GameTrainerLauncher.Core.Interfaces;
using GameTrainerLauncher.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;

namespace GameTrainerLauncher.UI.ViewModels;

public partial class MyGamesViewModel : ObservableObject
{
    private readonly AppDbContext _dbContext;
    private readonly ITrainerManager _trainerManager;
    private readonly IScraperService _scraperService;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private ObservableCollection<Game> _games = new();

    /// <summary>When set, only this Game row's download button is disabled (by reference, no shared state).</summary>
    [ObservableProperty]
    private Game? _currentDownloadingGame;

    /// <summary>When set, only this Game row's launch button shows loading/disabled.</summary>
    [ObservableProperty]
    private Game? _currentLaunchingGame;

    /// <summary>Whether there are any games in the collection.</summary>
    public bool HasGames => Games.Count > 0;

    /// <summary>True after the first LoadGamesAsync has completed (avoids showing empty state while loading).</summary>
    [ObservableProperty]
    private bool _isDataLoaded;

    /// <summary>Only show "暂无游戏" when we're on My Games and load finished with no data.</summary>
    public bool ShowNoGamesEmptyState => IsDataLoaded && !HasGames;

    partial void OnGamesChanged(ObservableCollection<Game> value)
    {
        OnPropertyChanged(nameof(HasGames));
        OnPropertyChanged(nameof(ShowNoGamesEmptyState));
    }

    partial void OnIsDataLoadedChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowNoGamesEmptyState));
    }

    public MyGamesViewModel(AppDbContext dbContext, ITrainerManager trainerManager, IScraperService scraperService, INavigationService navigationService)
    {
        _dbContext = dbContext;
        _trainerManager = trainerManager;
        _scraperService = scraperService;
        _navigationService = navigationService;
        // 加载与封面拉取由页面 Loaded 时统一触发，保证每次进入「我的游戏」都会检查并补封面
    }

    [RelayCommand]
    public void NavigateToPopular()
    {
        _navigationService.NavigateTo("Popular");
    }

    [RelayCommand]
    public async Task LoadGamesAsync()
    {
        // Ensure DB created
        try 
        {
            await _dbContext.Database.EnsureCreatedAsync();
            await _dbContext.MigrateTrainersTableDropIgnoredColumnsAsync();

            var dbGames = await _dbContext.Games.Include(g => g.MatchedTrainer).ToListAsync();

            // De-duplicate logic (though DB primary key handles ID, we want to ensure uniqueness by name for display)
            // Rebuild the observable collection
            Games.Clear();
            var uniqueGames = dbGames.GroupBy(g => g.Name).Select(g => g.First());
            foreach (var g in uniqueGames)
            {
                if (g.MatchedTrainer != null && g.MatchedTrainer.IsDownloaded &&
                    (string.IsNullOrEmpty(g.MatchedTrainer.LocalExePath) || !System.IO.File.Exists(g.MatchedTrainer.LocalExePath)))
                {
                    g.MatchedTrainer.IsDownloaded = false;
                    g.MatchedTrainer.LocalExePath = null;
                    g.MatchedTrainer.LocalZipPath = null;
                    _dbContext.Trainers.Update(g.MatchedTrainer);
                }
                Games.Add(g);
            }
            await _dbContext.SaveChangesAsync();

            // For locally scanned games (no cover / no trainer), fetch cover and date from Fling
            foreach (var game in Games.ToList())
            {
                var needCover = string.IsNullOrWhiteSpace(game.MatchedTrainer?.ImageUrl) && string.IsNullOrWhiteSpace(game.CoverUrl);
                if (!needCover) continue;
                try
                {
                    var results = await _scraperService.SearchAsync(game.Name);
                    var first = results.FirstOrDefault();
                    if (first == null) continue;
                    var details = await _scraperService.GetTrainerDetailsAsync(first.PageUrl);
                    if (game.MatchedTrainer == null)
                    {
                        var trainer = new Trainer
                        {
                            Title = details.Title,
                            PageUrl = details.PageUrl,
                            DownloadUrl = details.DownloadUrl,
                            ImageUrl = details.ImageUrl,
                            LastUpdated = details.LastUpdated,
                            IsDownloaded = false
                        };
                        _dbContext.Trainers.Add(trainer);
                        await _dbContext.SaveChangesAsync();
                        game.MatchedTrainerId = trainer.Id;
                        game.MatchedTrainer = trainer;
                        if (!string.IsNullOrWhiteSpace(details.ImageUrl)) game.CoverUrl = details.ImageUrl;
                        _dbContext.Games.Update(game);
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(game.MatchedTrainer.DownloadUrl) && !string.IsNullOrWhiteSpace(details.DownloadUrl))
                            game.MatchedTrainer.DownloadUrl = details.DownloadUrl;
                        if (string.IsNullOrWhiteSpace(game.MatchedTrainer.ImageUrl) && !string.IsNullOrWhiteSpace(details.ImageUrl))
                        {
                            game.MatchedTrainer.ImageUrl = details.ImageUrl;
                            game.CoverUrl = details.ImageUrl;
                        }
                        if (details.LastUpdated != null)
                            game.MatchedTrainer.LastUpdated = details.LastUpdated;
                        _dbContext.Trainers.Update(game.MatchedTrainer);
                        if (!string.IsNullOrWhiteSpace(game.CoverUrl))
                            _dbContext.Games.Update(game);
                    }
                    await _dbContext.SaveChangesAsync();
                }
                catch { /* ignore per-game fetch */ }
            }
            
            IsDataLoaded = true;
            OnPropertyChanged(nameof(HasGames));
            OnPropertyChanged(nameof(ShowNoGamesEmptyState));
        }
        catch (Exception ex)
        {
             IsDataLoaded = true;
             OnPropertyChanged(nameof(ShowNoGamesEmptyState));
             var title = GetString("MsgErrorTitle");
             var msg = GetString("MsgDatabaseError") + " " + ex.Message;
             System.Windows.MessageBox.Show(msg, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    /// <summary>Like Remove: command returns immediately, only UI trigger disables the one row (no shared command disabling).</summary>
    [RelayCommand]
    public void LaunchTrainer(Game game)
    {
        if (game?.MatchedTrainer == null)
        {
            var msg = GetString("MsgNoTrainerFound");
            var title = GetString("MsgErrorTitle");
            System.Windows.MessageBox.Show(msg, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            return;
        }
        CurrentLaunchingGame = game;
        _ = RunLaunchInBackgroundAsync(game);
    }

    private async Task RunLaunchInBackgroundAsync(Game game)
    {
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            game.MatchedTrainer!.IsLoading = true;
            await Task.Run(async () =>
            {
                await _trainerManager.LaunchTrainerAsync(game.MatchedTrainer!);
            }, cts.Token);
        }
        catch (OperationCanceledException)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                System.Windows.MessageBox.Show(GetString("MsgLaunchTimeout"), GetString("MsgTimeoutTitle"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            });
        }
        catch (Exception ex)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                System.Windows.MessageBox.Show(GetString("MsgLaunchFailed") + " " + ex.Message, GetString("MsgErrorTitle"), System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            });
        }
        finally
        {
            game.MatchedTrainer!.IsLoading = false;
            _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => CurrentLaunchingGame = null);
        }
    }

    [RelayCommand]
    public async Task RemoveGameAsync(Game game)
    {
        var msg = GetString("MsgConfirmRemoveBody", game.Name);
        var title = GetString("MsgConfirmRemoveTitle");
        var result = System.Windows.MessageBox.Show(msg, title, System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (result == System.Windows.MessageBoxResult.Yes)
        {
            try
            {
                Games.Remove(game);
                _dbContext.Games.Remove(game);
                await _dbContext.SaveChangesAsync();
                
                // Notify PopularGamesViewModel to update button state
                // In a real MVVM app, use Messenger/EventAggregator.
                // Here we can try to find the view model if registered as singleton, or rely on shared state.
                // Since PopularGamesViewModel reloads or checks DB, we might need to trigger a refresh.
                // However, PopularGamesViewModel loads data into memory.
                
                // For now, let's just save. If the user goes back to PopularGames, it might need to refresh.
                // But PopularGamesViewModel has IsDownloaded property on Trainer objects.
                // We need to find the trainer in PopularGamesViewModel and set IsDownloaded = false.
                
                // A quick hack for this simple architecture:
                // We can't easily reach other view models without a Messenger.
                // Let's assume the user will refresh or the app will reload.
                // But to satisfy the requirement "need to sync update", we should try.
                
                // If PopularGamesViewModel is Singleton (it is in App.xaml.cs), we can get it.
                var popularVM = (System.Windows.Application.Current as App)?.Services.GetService(typeof(PopularGamesViewModel)) as PopularGamesViewModel;
                if (popularVM != null)
                {
                    // Use case-insensitive matching and trim whitespace to handle slight differences
                    var trainer = popularVM.Trainers.FirstOrDefault(t => 
                        string.Equals(t.Title.Trim(), game.Name.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (trainer != null)
                    {
                        trainer.IsDownloaded = false;
                    }
                }
                
                OnPropertyChanged(nameof(HasGames));
                OnPropertyChanged(nameof(ShowNoGamesEmptyState));
            }
            catch (Exception ex)
            {
                 var removeMsg = GetString("MsgRemoveFailed") + " " + ex.Message;
                 var errTitle = GetString("MsgErrorTitle");
                 System.Windows.MessageBox.Show(removeMsg, errTitle, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    private string GetString(string key, params object[] args)
    {
        var resource = System.Windows.Application.Current.FindResource(key) as string;
        if (resource == null) return key;
        return args.Length > 0 ? string.Format(resource, args) : resource;
    }

    /// <summary>
    /// 点击「下载」后的完整流程：
    /// 1) 若当前游戏没有 MatchedTrainer：用游戏名 SearchAsync，取第一个结果 GetTrainerDetailsAsync，写入 DB 并赋给 game.MatchedTrainer。
    /// 2) 若有 Trainer 但无 DownloadUrl：用 PageUrl 调 GetTrainerDetailsAsync 补全 DownloadUrl/ImageUrl/LastUpdated 并保存。
    /// 3) 设置 CurrentDownloadingTrainerId、IsDownloading、DownloadProgress，调用 TrainerManager.DownloadTrainerAsync(trainer, progress)。
    /// 4) 若失败且存在 PageUrl：重新拉取详情更新 DownloadUrl 后重试一次。
    /// 5) 成功则设置 IsDownloaded、更新 DB；失败则弹窗 "Download failed"；finally 里清除 IsDownloading 和 CurrentDownloadingTrainerId。
    /// </summary>
    [RelayCommand]
    public async Task DownloadTrainerAsync(Game game)
    {
        if (game.MatchedTrainer == null)
        {
             var results = await _scraperService.SearchAsync(game.Name);
             var match = results.FirstOrDefault();
             if (match != null)
             {
                 var details = await _scraperService.GetTrainerDetailsAsync(match.PageUrl);
                 game.MatchedTrainer = details;
                 _dbContext.Games.Update(game);
                 await _dbContext.SaveChangesAsync();
             }
             else
             {
                 var msg = GetString("MsgTrainerNotFound", game.Name);
                 var title = GetString("MsgErrorTitle");
                 System.Windows.MessageBox.Show(msg, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                 return; 
             }
        }
        else if (game.MatchedTrainer != null && string.IsNullOrWhiteSpace(game.MatchedTrainer.DownloadUrl) && !string.IsNullOrWhiteSpace(game.MatchedTrainer.PageUrl))
        {
             var details = await _scraperService.GetTrainerDetailsAsync(game.MatchedTrainer.PageUrl);
             game.MatchedTrainer.DownloadUrl = details.DownloadUrl;
             game.MatchedTrainer.LastUpdated = details.LastUpdated;
             if (!string.IsNullOrEmpty(details.ImageUrl)) game.MatchedTrainer.ImageUrl = details.ImageUrl;
             _dbContext.Trainers.Update(game.MatchedTrainer);
             await _dbContext.SaveChangesAsync();
        }

        if (game.MatchedTrainer != null)
        {
            var trainer = game.MatchedTrainer;
            CurrentDownloadingGame = game;
            trainer.IsDownloading = true;
            trainer.DownloadProgress = 0;

            var progress = new Progress<double>(p => 
            {
                trainer.DownloadProgress = p;
            });
            
            try
            {
                var success = await _trainerManager.DownloadTrainerAsync(trainer, progress);
                if (!success && !string.IsNullOrWhiteSpace(trainer.PageUrl))
                {
                    try
                    {
                        var details = await _scraperService.GetTrainerDetailsAsync(trainer.PageUrl);
                        if (!string.IsNullOrWhiteSpace(details.DownloadUrl))
                        {
                            trainer.DownloadUrl = details.DownloadUrl;
                            trainer.LastUpdated = details.LastUpdated;
                            if (!string.IsNullOrEmpty(details.ImageUrl)) trainer.ImageUrl = details.ImageUrl;
                            _dbContext.Trainers.Update(trainer);
                            await _dbContext.SaveChangesAsync();
                            success = await _trainerManager.DownloadTrainerAsync(trainer, progress);
                        }
                    }
                    catch { /* ignore retry fetch */ }
                }
                if (success)
                {
                    trainer.IsDownloaded = true;
                    _dbContext.Trainers.Update(trainer);
                    _dbContext.Games.Update(game);
                    await _dbContext.SaveChangesAsync();
                }
                else
                {
                    var msg = GetString("MsgDownloadFailed");
                    var title = GetString("MsgErrorTitle");
                    System.Windows.MessageBox.Show(msg, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                var title = GetString("MsgErrorTitle");
                System.Windows.MessageBox.Show(GetString("MsgErrorWithDetail", ex.Message), title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                trainer.IsDownloading = false;
                _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => CurrentDownloadingGame = null);
            }
        }
    }
}
