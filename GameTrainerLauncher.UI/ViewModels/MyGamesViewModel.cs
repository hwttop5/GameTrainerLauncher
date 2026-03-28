using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Models;
using GameTrainerLauncher.Core.Utilities;
using GameTrainerLauncher.UI;
using GameTrainerLauncher.UI.Services;
using GameTrainerLauncher.Core.Interfaces;
using GameTrainerLauncher.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using Wpf.Ui.Controls;

namespace GameTrainerLauncher.UI.ViewModels;

public partial class MyGamesViewModel : PageFeedbackViewModelBase
{
    private readonly AppDbContext _dbContext;
    private readonly ITrainerManager _trainerManager;
    private readonly IScraperService _scraperService;
    private readonly INavigationService _navigationService;
    private readonly IGameCoverService _coverService;
    private readonly IAppNotificationService _notificationService;

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

    public MyGamesViewModel(
        AppDbContext dbContext,
        ITrainerManager trainerManager,
        IScraperService scraperService,
        INavigationService navigationService,
        IGameCoverService coverService,
        IAppNotificationService notificationService)
    {
        _dbContext = dbContext;
        _trainerManager = trainerManager;
        _scraperService = scraperService;
        _navigationService = navigationService;
        _coverService = coverService;
        _notificationService = notificationService;
        // 加载与封面拉取由页面 Loaded 时统一触发，保证每次进入「我的游戏」都会检查并补封面
    }

    [RelayCommand]
    public void NavigateToPopular()
    {
        _navigationService.NavigateTo("Popular");
    }

    /// <summary>拖拽排序：将游戏从 oldIndex 移到 newIndex，并持久化 DisplayOrder。</summary>
    public void MoveGameByIndex(int oldIndex, int newIndex)
    {
        if (oldIndex < 0 || newIndex < 0 || oldIndex >= Games.Count || newIndex >= Games.Count || oldIndex == newIndex)
            return;
        var game = Games[oldIndex];
        Games.RemoveAt(oldIndex);
        Games.Insert(newIndex, game);
        _ = SaveDisplayOrderFromCurrentListAsync();
    }

    /// <summary>按当前 Games 顺序将 DisplayOrder 写回数据库。</summary>
    public async Task SaveDisplayOrderFromCurrentListAsync()
    {
        try
        {
            for (var i = 0; i < Games.Count; i++)
            {
                Games[i].DisplayOrder = i;
                _dbContext.Games.Update(Games[i]);
            }
            await _dbContext.SaveChangesAsync();
        }
        catch { /* ignore */ }
    }

    [RelayCommand]
    public async Task LoadGamesAsync()
    {
        // Ensure DB created
        try 
        {
            ClearPageFeedback();
            await _dbContext.Database.EnsureCreatedAsync();
            await _dbContext.MigrateTrainersTableDropIgnoredColumnsAsync();
            await _dbContext.EnsureGamesDisplayOrderColumnAsync();

            var dbGames = await _dbContext.Games.Include(g => g.MatchedTrainer).ToListAsync();

            // De-duplicate and fix invalid IsDownloaded
            var uniqueGames = dbGames.GroupBy(g => g.Name).Select(g => g.First()).ToList();
            // 默认倒序：最新添加在前；若有自定义排序（DisplayOrder 已设）则按 DisplayOrder 升序，其余按 AddedDate 倒序
            uniqueGames = uniqueGames
                .OrderBy(g => g.DisplayOrder.HasValue ? 0 : 1)
                .ThenBy(g => g.DisplayOrder ?? int.MaxValue)
                .ThenByDescending(g => g.AddedDate ?? DateTime.MinValue)
                .ToList();
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
            }
            await _dbContext.SaveChangesAsync();

            // 必须在 UI 线程更新 ObservableCollection 和 IsDataLoaded，否则打包后可能列表与“暂无游戏”状态不同步
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Games.Clear();
                foreach (var g in uniqueGames)
                    Games.Add(g);
                IsDataLoaded = true;
                OnPropertyChanged(nameof(HasGames));
                OnPropertyChanged(nameof(ShowNoGamesEmptyState));
            });

            // 进入「我的游戏」时：只读本地封面；若缺失则抓取 URL 并下载到本地
            var coverUpdatedCount = 0;
            var coverDownloadedCount = 0;
            foreach (var game in Games.ToList())
            {
                try
                {
                    var gameId = game.Id;
                    var hasLocalCover = gameId > 0 && _coverService.HasCover(gameId);

                    // 先看 DB 是否已有 URL
                    var coverUrl = game.MatchedTrainer?.ImageUrl ?? game.CoverUrl;

                    // 若本地缺封面且也没有 URL：去网站抓取并写回 DB
                    if (!hasLocalCover && string.IsNullOrWhiteSpace(coverUrl))
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
                                Version = details.Version,
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
                            coverUpdatedCount++;
                        }
                        else
                        {
                            if (string.IsNullOrWhiteSpace(game.MatchedTrainer.DownloadUrl) || string.IsNullOrWhiteSpace(game.MatchedTrainer.Version))
                                ApplyResolvedTrainerDetails(game.MatchedTrainer, details, preferExistingVersion: true);
                            if (string.IsNullOrWhiteSpace(game.MatchedTrainer.ImageUrl) && !string.IsNullOrWhiteSpace(details.ImageUrl))
                            {
                                game.MatchedTrainer.ImageUrl = details.ImageUrl;
                                game.CoverUrl = details.ImageUrl;
                                coverUpdatedCount++;
                            }
                            if (details.LastUpdated != null)
                                game.MatchedTrainer.LastUpdated = details.LastUpdated;
                            _dbContext.Trainers.Update(game.MatchedTrainer);
                            if (!string.IsNullOrWhiteSpace(game.CoverUrl))
                                _dbContext.Games.Update(game);
                        }
                        await _dbContext.SaveChangesAsync();

                        coverUrl = game.MatchedTrainer?.ImageUrl ?? game.CoverUrl;
                    }

                    // 若本地缺封面但已有 URL：下载到本地
                    if (gameId > 0 && !_coverService.HasCover(gameId) && !string.IsNullOrWhiteSpace(coverUrl))
                    {
                        var ok = await _coverService.EnsureCoverAsync(gameId, coverUrl);
                        if (ok) coverDownloadedCount++;
                    }
                }
                catch { /* ignore per-game fetch */ }
            }

            // 若 URL 或本地封面在加载后补齐：因为 EF 实体不触发 PropertyChanged，刷新集合以触发封面绑定重新计算
            if (coverUpdatedCount > 0 || coverDownloadedCount > 0)
            {
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    Games = new ObservableCollection<Game>(Games);
                });
            }
        }
        catch (Exception ex)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                IsDataLoaded = true;
                OnPropertyChanged(nameof(ShowNoGamesEmptyState));
                ShowPageFeedback(
                    InfoBarSeverity.Error,
                    GetString("MsgErrorTitle"),
                    GetString("MsgDatabaseError") + " " + ex.Message);
            });
        }
    }

    /// <summary>Like Remove: command returns immediately, only UI trigger disables the one row (no shared command disabling).</summary>
    [RelayCommand]
    public void LaunchTrainer(Game game)
    {
        if (game?.MatchedTrainer == null)
        {
            ShowPageFeedback(InfoBarSeverity.Error, GetString("MsgErrorTitle"), GetString("MsgNoTrainerFound"));
            return;
        }
        ClearPageFeedback();
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
                ShowPageFeedback(InfoBarSeverity.Warning, GetString("MsgWarningTitle"), GetString("MsgLaunchTimeout"));
            });
        }
        catch (Exception ex)
        {
            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ShowPageFeedback(InfoBarSeverity.Error, GetString("MsgErrorTitle"), GetString("MsgLaunchFailed") + " " + ex.Message);
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
                // 移除时：封面文件一并删除（失败不影响移除）
                try { if (game.Id > 0) _coverService.DeleteCover(game.Id); } catch { }

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
                 ShowPageFeedback(InfoBarSeverity.Error, GetString("MsgErrorTitle"), GetString("MsgRemoveFailed") + " " + ex.Message);
            }
        }
    }

    private string GetString(string key, params object[] args)
    {
        var resource = System.Windows.Application.Current.FindResource(key) as string;
        if (resource == null) return key;
        return args.Length > 0 ? string.Format(resource, args) : resource;
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

    private static void ApplyResolvedTrainerDetails(Trainer trainer, Trainer details, bool preferExistingVersion)
    {
        trainer.DownloadOptions = details.DownloadOptions
            .OrderBy(option => option.SortOrder)
            .ToList();

        if (!string.IsNullOrEmpty(details.ImageUrl))
        {
            trainer.ImageUrl = details.ImageUrl;
        }

        var matchedOption = preferExistingVersion
            ? TrainerSelectionHelpers.FindMatchingOption(trainer.DownloadOptions, trainer.Version)
            : null;

        if (matchedOption != null)
        {
            TrainerSelectionHelpers.ApplyDownloadOption(trainer, matchedOption);
            return;
        }

        if (!string.IsNullOrWhiteSpace(details.DownloadUrl))
        {
            trainer.DownloadUrl = details.DownloadUrl;
        }

        if (!string.IsNullOrWhiteSpace(details.Version))
        {
            trainer.Version = details.Version;
        }

        if (details.LastUpdated != null)
        {
            trainer.LastUpdated = details.LastUpdated;
        }
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
        ClearPageFeedback();
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
                 ShowPageFeedback(InfoBarSeverity.Error, GetString("MsgErrorTitle"), GetString("MsgTrainerNotFound", game.Name));
                 return; 
             }
        }
        else if (game.MatchedTrainer != null && string.IsNullOrWhiteSpace(game.MatchedTrainer.DownloadUrl) && !string.IsNullOrWhiteSpace(game.MatchedTrainer.PageUrl))
        {
             var details = await _scraperService.GetTrainerDetailsAsync(game.MatchedTrainer.PageUrl);
             ApplyResolvedTrainerDetails(game.MatchedTrainer, details, preferExistingVersion: true);
             _dbContext.Trainers.Update(game.MatchedTrainer);
             await _dbContext.SaveChangesAsync();
        }

        if (game.MatchedTrainer != null)
        {
            var trainer = game.MatchedTrainer;
            CurrentDownloadingGame = game;
            trainer.IsDownloading = true;
            ResetDownloadProgress(trainer);
            trainer.DownloadStatusText = "Preparing download...";
            trainer.IsDownloadProgressEstimated = true;

            var progress = new Progress<TrainerDownloadProgress>(update => 
            {
                ApplyDownloadProgress(trainer, update);
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
                            ApplyResolvedTrainerDetails(trainer, details, preferExistingVersion: true);
                            _dbContext.Trainers.Update(trainer);
                            await _dbContext.SaveChangesAsync();
                            success = await _trainerManager.DownloadTrainerAsync(trainer, progress);
                        }
                    }
                    catch { /* ignore retry fetch */ }
                }
                if (success)
                {
                    trainer.DownloadProgress = 100;
                    trainer.DownloadStatusText = "Completed.";
                    trainer.IsDownloadProgressEstimated = false;
                    trainer.DownloadStage = TrainerDownloadStage.Finalizing;
                    await Task.Delay(250);
                    trainer.IsDownloaded = true;
                    _dbContext.Trainers.Update(trainer);
                    _dbContext.Games.Update(game);
                    await _dbContext.SaveChangesAsync();
                    _notificationService.ShowSuccess(GetString("MsgDownloadSuccess"));
                }
                else
                {
                    ShowPageFeedback(InfoBarSeverity.Error, GetString("MsgErrorTitle"), GetString("MsgDownloadFailed"));
                }
            }
            catch (Exception ex)
            {
                ShowPageFeedback(InfoBarSeverity.Error, GetString("MsgErrorTitle"), GetString("MsgErrorWithDetail", ex.Message));
            }
            finally
            {
                ResetDownloadProgress(trainer);
                trainer.IsDownloading = false;
                _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => CurrentDownloadingGame = null);
            }
        }
    }
}
