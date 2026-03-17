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

namespace GameTrainerLauncher.UI;

public partial class App : Application
{
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

        // UI Services
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<IMyGamesRefreshService, MyGamesRefreshService>();

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

        var themeService = Services.GetRequiredService<IThemeService>();
        themeService.Initialize();

        var mainWindow = Services.GetRequiredService<MainWindow>();
        mainWindow.Show();

        // 启动时后台拉取「我的游戏」中缺失的封面，避免之前添加的游戏不显示封面
        _ = RunStartupCoverFetchAsync();
    }

    private async Task RunStartupCoverFetchAsync()
    {
        try
        {
            using var scope = Services.GetRequiredService<IServiceScopeFactory>().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var scraper = scope.ServiceProvider.GetRequiredService<IScraperService>();
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
