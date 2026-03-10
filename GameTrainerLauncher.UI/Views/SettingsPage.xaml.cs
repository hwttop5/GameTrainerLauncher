using GameTrainerLauncher.UI.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Controls;
using Wpf.Ui.Appearance;

namespace GameTrainerLauncher.UI.Views;

public partial class SettingsPage : Page
{
    private readonly IThemeService _themeService;

    public SettingsPage()
    {
        InitializeComponent();
        _themeService = ((App)Application.Current).Services.GetRequiredService<IThemeService>();
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
}
