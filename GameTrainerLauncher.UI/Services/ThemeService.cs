using System.Linq;
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
    private readonly IAppSettingsService _settingsService;

    public ThemeService(IAppSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public void Initialize()
    {
        var currentSettings = _settingsService.GetSettings();

        var theme = currentSettings.Theme == "Light" ? ApplicationTheme.Light : ApplicationTheme.Dark;
        ApplicationThemeManager.Apply(theme);
        ApplyThemeBrushes(theme);
        SetLanguageInternal(currentSettings.Language);
    }

    public void SetTheme(ApplicationTheme theme)
    {
        ApplicationThemeManager.Apply(theme);
        ApplyThemeBrushes(theme);
        _settingsService.Update(next => next.Theme = theme == ApplicationTheme.Light ? "Light" : "Dark");
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
        var emptyStateTitleColor = isLight ? Color.FromRgb(0x1e, 0x1e, 0x1e) : Colors.White;
        var app = Application.Current;
        if (app?.Resources == null) return;
        app.Resources["EmptyStateTitleBrush"] = new SolidColorBrush(emptyStateTitleColor);
        app.Resources["SystemControlBackgroundChromeMediumLowBrush"] = new SolidColorBrush(sidebarColor);
        app.Resources["ApplicationBackgroundBrush"] = new SolidColorBrush(bgColor);
        app.Resources["WindowBackground"] = new SolidColorBrush(bgColor);
        app.Resources["WindowBackgroundColor"] = bgColor;
        app.Resources["CoverPlaceholderBrush"] = new SolidColorBrush(coverPlaceholderColor);
        app.Resources["SecondaryTextBrush"] = new SolidColorBrush(secondaryTextColor);
        app.Resources["CardBackgroundBrush"] = new SolidColorBrush(cardBgColor);
        app.Resources["CardBorderBrush"] = new SolidColorBrush(cardBorderColor);
        app.Resources["ListItemHoverBrush"] = new SolidColorBrush(listItemHoverColor);
        // 搜索按钮固定蓝底白字，不随主题变
        app.Resources["SearchButtonBackgroundBrush"] = new SolidColorBrush(Color.FromRgb(0x00, 0x78, 0xD4));
        app.Resources["SearchButtonForegroundBrush"] = new SolidColorBrush(Colors.White);
    }

    public void SetLanguage(string languageCode)
    {
        SetLanguageInternal(languageCode);
        _settingsService.Update(next => next.Language = languageCode);
    }

    public string GetCurrentLanguage() => _settingsService.GetSettings().Language;

    public string GetCurrentTheme() => _settingsService.GetSettings().Theme;

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
}
