using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Appearance;

namespace GameTrainerLauncher.UI.Services;

public interface IThemeService
{
    void Initialize();
    void SetTheme(ApplicationTheme theme);
    void SetLanguage(string languageCode);
    string GetCurrentLanguage();
    string GetCurrentTheme();
}

public class ThemeService : IThemeService
{
    private class UserSettings
    {
        public string Theme { get; set; } = "Dark";
        public string Language { get; set; } = "en-US";
    }

    private readonly string? _settingsPath;
    private UserSettings _currentSettings;

    public ThemeService()
    {
        try
        {
            var appFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            Directory.CreateDirectory(appFolder);
            _settingsPath = Path.Combine(appFolder, "settings.json");
        }
        catch (Exception)
        {
            _settingsPath = null;
        }
        _currentSettings = new UserSettings();
    }

    public void Initialize()
    {
        if (_settingsPath != null && File.Exists(_settingsPath))
        {
            try
            {
                var json = File.ReadAllText(_settingsPath);
                var loaded = JsonSerializer.Deserialize<UserSettings>(json);
                if (loaded != null)
                {
                    _currentSettings = loaded;
                }
            }
            catch
            {
                // Use defaults on load error
            }
        }

        var theme = _currentSettings.Theme == "Light" ? ApplicationTheme.Light : ApplicationTheme.Dark;
        ApplicationThemeManager.Apply(theme);
        ApplyThemeBrushes(theme);
        SetLanguageInternal(_currentSettings.Language);
    }

    public void SetTheme(ApplicationTheme theme)
    {
        ApplicationThemeManager.Apply(theme);
        ApplyThemeBrushes(theme);
        _currentSettings.Theme = theme == ApplicationTheme.Light ? "Light" : "Dark";
        SaveSettings();
    }

    /// <summary>Update sidebar, window and card-related brushes for light/dark theme.</summary>
    private static void ApplyThemeBrushes(ApplicationTheme theme)
    {
        var isLight = theme == ApplicationTheme.Light;
        var sidebarColor = isLight ? Color.FromRgb(0xf3, 0xf3, 0xf3) : Color.FromRgb(0x1e, 0x20, 0x21);
        var bgColor = isLight ? Color.FromRgb(0xfa, 0xfa, 0xfa) : Color.FromRgb(0x1e, 0x20, 0x21);
        var coverPlaceholderColor = isLight ? Color.FromRgb(0xe8, 0xe8, 0xe8) : Color.FromRgb(0x2a, 0x2a, 0x2a);
        var secondaryTextColor = isLight ? Color.FromRgb(0x60, 0x60, 0x60) : Color.FromRgb(0x80, 0x80, 0x80);
        var cardBgColor = isLight ? Color.FromRgb(0xff, 0xff, 0xff) : Color.FromRgb(0x25, 0x25, 0x25);
        var cardBorderColor = isLight ? Color.FromRgb(0xe0, 0xe0, 0xe0) : Color.FromRgb(0x3a, 0x3a, 0x3a);
        var listItemHoverColor = isLight ? Color.FromRgb(0xe8, 0xe8, 0xe8) : Color.FromRgb(0x2d, 0x2d, 0x2d);
        var app = Application.Current;
        if (app?.Resources == null) return;
        app.Resources["SystemControlBackgroundChromeMediumLowBrush"] = new SolidColorBrush(sidebarColor);
        app.Resources["ApplicationBackgroundBrush"] = new SolidColorBrush(bgColor);
        app.Resources["WindowBackground"] = new SolidColorBrush(bgColor);
        app.Resources["WindowBackgroundColor"] = bgColor;
        app.Resources["CoverPlaceholderBrush"] = new SolidColorBrush(coverPlaceholderColor);
        app.Resources["SecondaryTextBrush"] = new SolidColorBrush(secondaryTextColor);
        app.Resources["CardBackgroundBrush"] = new SolidColorBrush(cardBgColor);
        app.Resources["CardBorderBrush"] = new SolidColorBrush(cardBorderColor);
        app.Resources["ListItemHoverBrush"] = new SolidColorBrush(listItemHoverColor);
    }

    public void SetLanguage(string languageCode)
    {
        SetLanguageInternal(languageCode);
        _currentSettings.Language = languageCode;
        SaveSettings();
    }

    public string GetCurrentLanguage() => _currentSettings.Language;

    public string GetCurrentTheme() => _currentSettings.Theme;

    private void SetLanguageInternal(string languageCode)
    {
        var dict = new ResourceDictionary();
        switch (languageCode)
        {
            case "zh-CN":
                dict.Source = new Uri("Resources/Languages/Chinese.xaml", UriKind.Relative);
                break;
            default:
                dict.Source = new Uri("Resources/Languages/English.xaml", UriKind.Relative);
                break;
        }

        var merged = Application.Current.Resources.MergedDictionaries;
        // Find the language dictionary (heuristic: it has specific keys)
        var langDict = merged.FirstOrDefault(d => d.Source != null && d.Source.ToString().Contains("Languages"));
        if (langDict != null)
        {
            merged.Remove(langDict);
        }
        merged.Add(dict);
    }

    private void SaveSettings()
    {
        if (_settingsPath == null) return;

        try
        {
            var json = JsonSerializer.Serialize(_currentSettings);
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
            // Ignore errors
        }
    }
}
