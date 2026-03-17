using GameTrainerLauncher.Core.Interfaces;
using GameTrainerLauncher.Infrastructure.Data;
using GameTrainerLauncher.Infrastructure.Services;
using GameTrainerLauncher.UI.Services;
using GameTrainerLauncher.UI.ViewModels;
using GameTrainerLauncher.UI.Views;
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

        // Configure NLog：日志写在 程序目录\Data\Logs\log.txt，确保目录存在
        var logDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Logs");
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
