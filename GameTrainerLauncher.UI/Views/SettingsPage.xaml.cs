using GameTrainerLauncher.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Appearance;

namespace GameTrainerLauncher.UI.Views;

public partial class SettingsPage : Page
{
    private const string RepositoryUrl = "https://github.com/hwttop5/GameTrainerLauncher";
    private const string ReleasesUrl = "https://github.com/hwttop5/GameTrainerLauncher/releases";
    private readonly IThemeService _themeService;
    private readonly IAppUpdateService _appUpdateService;
    private readonly IAppNotificationService _notificationService;

    public SettingsPage(IAppUpdateService appUpdateService, IAppNotificationService notificationService)
    {
        InitializeComponent();
        _themeService = ((App)Application.Current).Services.GetRequiredService<IThemeService>();
        _appUpdateService = appUpdateService;
        _notificationService = notificationService;
        LoadCurrentSettings();
    }

    private void LoadCurrentSettings()
    {
        var lang = _themeService.GetCurrentLanguage();
        LangEn.IsChecked = lang != "zh-CN";
        LangZh.IsChecked = lang == "zh-CN";
        var theme = _themeService.GetCurrentTheme();
        ThemeLight.IsChecked = theme == "Light";
        ThemeDark.IsChecked = theme == "Dark";
        RefreshUpdateStatus();
    }

    private void Language_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string lang)
            _themeService.SetLanguage(lang);
    }

    private void Theme_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton rb && rb.Tag is string themeStr)
        {
            var theme = themeStr == "Dark" ? ApplicationTheme.Dark : ApplicationTheme.Light;
            _themeService.SetTheme(theme);
        }
    }

    private async void CheckForUpdates_Click(object sender, RoutedEventArgs e)
    {
        CheckForUpdatesButton.IsEnabled = false;
        UpdateStatusText.Text = UpdateTextFormatter.GetString("UpdateChecking");

        try
        {
            var result = await _appUpdateService.CheckForUpdatesAsync(manual: true);
            RefreshUpdateStatus();
            await AppUpdateFlow.HandleCheckResultAsync(Window.GetWindow(this), _appUpdateService, _notificationService, result, manual: true);
            RefreshUpdateStatus();
        }
        finally
        {
            CheckForUpdatesButton.IsEnabled = true;
        }
    }

    private void RefreshUpdateStatus()
    {
        var snapshot = _appUpdateService.GetStatusSnapshot();
        CurrentVersionText.Text = snapshot.CurrentVersion;
        UpdateStatusText.Text = UpdateTextFormatter.GetStatusText(snapshot);
        UpdateLastCheckedText.Text = UpdateTextFormatter.FormatLastChecked(snapshot.LastCheckedAtUtc);
    }

    private void OpenRepo_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = RepositoryUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            var message = UpdateTextFormatter.Format("MsgNavigationError", ex.Message);
            _notificationService.ShowError(message);
        }
    }

    private void OpenReleases_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = ReleasesUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            var message = UpdateTextFormatter.Format("MsgNavigationError", ex.Message);
            _notificationService.ShowError(message);
        }
    }
}
