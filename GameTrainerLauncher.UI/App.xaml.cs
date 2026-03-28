using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Interfaces;
using GameTrainerLauncher.Infrastructure;
using GameTrainerLauncher.Infrastructure.Data;
using GameTrainerLauncher.Infrastructure.Services;
using GameTrainerLauncher.UI.Services;
using GameTrainerLauncher.UI.ViewModels;
using GameTrainerLauncher.UI.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Windows.Threading;
using System.Windows;
using NLog;

namespace GameTrainerLauncher.UI;

public partial class App : Application
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    public new static App Current => (App)Application.Current;
    public IServiceProvider Services { get; }

    public App()
    {
        Services = ConfigureServices();
        this.DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Core Services
        services.AddDbContext<AppDbContext>();
        services.AddSingleton<IScraperService, FlingScraperService>();
        services.AddSingleton<ITrainerManager, TrainerManager>();
        services.AddSingleton<IGameCoverService, GameCoverService>();

        // UI Services
        services.AddSingleton<IAppSettingsService, AppSettingsService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IAppUpdateService, AppUpdateService>();
        services.AddSingleton<IShortcutRepairService, ShortcutRepairService>();
        services.AddSingleton<IMyGamesRefreshService, MyGamesRefreshService>();
        services.AddSingleton<ITrainerLibraryService, TrainerLibraryService>();
        services.AddSingleton<ITrainerVersionSelectionService, TrainerVersionSelectionService>();

        // ViewModels
        services.AddTransient<MainViewModel>();
        services.AddSingleton<PopularGamesViewModel>(); // Changed to Singleton to share state
        services.AddTransient<MyGamesViewModel>();
        services.AddSingleton<SearchViewModel>(); // Changed to Singleton to share state with MainViewModel

        // Views
        services.AddTransient<MainWindow>();
        services.AddTransient<PopularGamesPage>();
        services.AddTransient<MyGamesPage>();
        services.AddTransient<SearchPage>();
        services.AddTransient<SettingsPage>();

        return services.BuildServiceProvider();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configure NLog：日志写在 %LocalAppData%\GameTrainerLauncher\Data\Logs\log.txt
        var logDir = System.IO.Path.Combine(AppPaths.DataFolder, "Logs");
        try { System.IO.Directory.CreateDirectory(logDir); } catch { }
        var config = new NLog.Config.LoggingConfiguration();
        var logfile = new NLog.Targets.FileTarget("logfile") { FileName = System.IO.Path.Combine(logDir, "log.txt") };
        var logconsole = new NLog.Targets.ConsoleTarget("logconsole");
        config.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, logconsole);
        config.AddRule(NLog.LogLevel.Debug, NLog.LogLevel.Fatal, logfile);
        NLog.LogManager.Configuration = config;

        var proxyUrl = ProxyEnvironmentBootstrapper.Configure();
        if (!string.IsNullOrWhiteSpace(proxyUrl))
        {
            Logger.Info("Configured local HTTP proxy for outbound requests: {ProxyUrl}", proxyUrl);
        }

        var themeService = Services.GetRequiredService<IThemeService>();
        themeService.Initialize();

        var shortcutRepairService = Services.GetRequiredService<IShortcutRepairService>();
        shortcutRepairService.RepairInstalledShortcuts();

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        _ = RunStartupUpdateCheckAsync(mainWindow);

        // 启动时后台拉取「我的游戏」中缺失的封面，避免之前添加的游戏不显示封面
        _ = RunStartupCoverFetchAsync();
    }

    private async Task RunStartupUpdateCheckAsync(Window owner)
    {
        try
        {
            var updateService = Services.GetRequiredService<IAppUpdateService>();
            var result = await updateService.CheckForUpdatesAsync(manual: false);
            await AppUpdateFlow.HandleCheckResultAsync(owner, updateService, result, manual: false);
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Startup update check failed.");
        }
    }

    private async Task RunStartupCoverFetchAsync()
    {
        try
        {
            using var scope = Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var scraper = scope.ServiceProvider.GetRequiredService<IScraperService>();
            var coverService = scope.ServiceProvider.GetRequiredService<IGameCoverService>();
            await db.Database.EnsureCreatedAsync();
            var games = await db.Games.Include(g => g.MatchedTrainer)
                .Where(g => (g.MatchedTrainer == null || string.IsNullOrWhiteSpace(g.MatchedTrainer.ImageUrl)) && string.IsNullOrWhiteSpace(g.CoverUrl))
                .ToListAsync();
            foreach (var game in games)
            {
                try
                {
                    var results = await scraper.SearchAsync(game.Name);
                    var first = results.FirstOrDefault();
                    if (first == null) continue;
                    var details = await scraper.GetTrainerDetailsAsync(first.PageUrl);
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
                        db.Trainers.Add(trainer);
                        await db.SaveChangesAsync();
                        game.MatchedTrainerId = trainer.Id;
                        game.MatchedTrainer = trainer;
                        if (!string.IsNullOrWhiteSpace(details.ImageUrl)) game.CoverUrl = details.ImageUrl;
                        db.Games.Update(game);
                    }
                    else
                    {
                        if (string.IsNullOrWhiteSpace(game.MatchedTrainer.DownloadUrl) && !string.IsNullOrWhiteSpace(details.DownloadUrl))
                            game.MatchedTrainer.DownloadUrl = details.DownloadUrl;
                        if (string.IsNullOrWhiteSpace(game.MatchedTrainer.Version) && !string.IsNullOrWhiteSpace(details.Version))
                            game.MatchedTrainer.Version = details.Version;
                        if (string.IsNullOrWhiteSpace(game.MatchedTrainer.ImageUrl) && !string.IsNullOrWhiteSpace(details.ImageUrl))
                        {
                            game.MatchedTrainer.ImageUrl = details.ImageUrl;
                            game.CoverUrl = details.ImageUrl;
                        }
                        if (details.LastUpdated != null)
                            game.MatchedTrainer.LastUpdated = details.LastUpdated;
                        db.Trainers.Update(game.MatchedTrainer);
                        if (!string.IsNullOrWhiteSpace(game.CoverUrl))
                            db.Games.Update(game);
                    }
                    await db.SaveChangesAsync();
                }
                catch { /* ignore per-game */ }
            }

            // 额外：若已有 URL，但本地缺封面，则补下载（不阻塞 UI，失败忽略）
            try
            {
                var needDownload = await db.Games.Include(g => g.MatchedTrainer)
                    .Where(g => !string.IsNullOrWhiteSpace(g.CoverUrl) || (g.MatchedTrainer != null && !string.IsNullOrWhiteSpace(g.MatchedTrainer.ImageUrl)))
                    .ToListAsync();
                foreach (var g in needDownload)
                {
                    if (g.Id <= 0) continue;
                    if (coverService.HasCover(g.Id)) continue;
                    var url = g.MatchedTrainer?.ImageUrl ?? g.CoverUrl;
                    if (string.IsNullOrWhiteSpace(url)) continue;
                    _ = coverService.EnsureCoverAsync(g.Id, url);
                }
            }
            catch { /* ignore */ }
        }
        catch { /* ignore startup fetch */ }
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.Exception.ToString(), "Unhandled UI Exception");
        e.Handled = true;
    }

    private void OnDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
    {
        MessageBox.Show(e.ExceptionObject?.ToString() ?? "Unknown error", "Unhandled Domain Exception");
    }
}
