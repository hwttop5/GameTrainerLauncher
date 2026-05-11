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
        var sidebarColor = isLight ? Color.FromRgb(0xF3, 0xF6, 0xF9) : Color.FromRgb(0x11, 0x16, 0x1B);
        var sidebarBorderColor = isLight ? Color.FromRgb(0xD8, 0xE1, 0xEA) : Color.FromRgb(0x25, 0x31, 0x3B);
        var bgColor = isLight ? Color.FromRgb(0xF7, 0xFA, 0xFC) : Color.FromRgb(0x17, 0x1B, 0x1F);
        var surfaceColor = isLight ? Colors.White : Color.FromRgb(0x1F, 0x24, 0x29);
        var surfaceRaisedColor = isLight ? Color.FromRgb(0xFF, 0xFF, 0xFF) : Color.FromRgb(0x25, 0x2B, 0x31);
        var surfaceHoverColor = isLight ? Color.FromRgb(0xEC, 0xF2, 0xF7) : Color.FromRgb(0x2A, 0x32, 0x39);
        var surfacePressedColor = isLight ? Color.FromRgb(0xDF, 0xE8, 0xF0) : Color.FromRgb(0x20, 0x26, 0x2C);
        var subtleSurfaceColor = isLight ? Color.FromRgb(0xF1, 0xF5, 0xF8) : Color.FromRgb(0x1A, 0x20, 0x26);
        var coverPlaceholderColor = isLight ? Color.FromRgb(0xE6, 0xEC, 0xF2) : Color.FromRgb(0x20, 0x26, 0x2C);
        var secondaryTextColor = isLight ? Color.FromRgb(0x5E, 0x6A, 0x75) : Color.FromRgb(0x9A, 0xA7, 0xB2);
        var strongTextColor = isLight ? Color.FromRgb(0x1C, 0x27, 0x31) : Color.FromRgb(0xF3, 0xF7, 0xFA);
        var faintTextColor = isLight ? Color.FromRgb(0x8A, 0x96, 0xA0) : Color.FromRgb(0x64, 0x71, 0x7C);
        var cardBgColor = surfaceColor;
        var cardBorderColor = isLight ? Color.FromRgb(0xD8, 0xE1, 0xEA) : Color.FromRgb(0x2D, 0x36, 0x3F);
        var listItemHoverColor = surfaceHoverColor;
        var emptyStateTitleColor = isLight ? Color.FromRgb(0x1e, 0x1e, 0x1e) : Colors.White;
        var notificationSurfaceColor = surfaceColor;
        var notificationBorderColor = cardBorderColor;
        var notificationTitleColor = strongTextColor;
        var notificationMessageColor = isLight ? Color.FromRgb(0x4F, 0x5B, 0x66) : Color.FromRgb(0xC7, 0xD0, 0xD8);
        var notificationCloseColor = isLight ? Color.FromRgb(0x7E, 0x8A, 0x94) : Color.FromRgb(0x94, 0xA1, 0xAD);
        var notificationShadowColor = isLight ? Color.FromArgb(0x26, 0x00, 0x00, 0x00) : Color.FromArgb(0x77, 0x00, 0x00, 0x00);
        var elevatedShadowColor = isLight ? Color.FromArgb(0x24, 0x00, 0x00, 0x00) : Color.FromArgb(0x66, 0x00, 0x00, 0x00);
        var infoColor = Color.FromRgb(0x2D, 0x9C, 0xFF);
        var infoHoverColor = Color.FromRgb(0x1D, 0x86, 0xDF);
        var infoPressedColor = Color.FromRgb(0x12, 0x6C, 0xB8);
        var primaryButtonColor = Color.FromRgb(0x12, 0x68, 0xAE);
        var primaryButtonHoverColor = Color.FromRgb(0x17, 0x77, 0xC2);
        var primaryButtonPressedColor = Color.FromRgb(0x0D, 0x56, 0x8F);
        var successColor = Color.FromRgb(0x30, 0xC9, 0x78);
        var successSoftColor = isLight ? Color.FromRgb(0xE7, 0xF8, 0xEF) : Color.FromRgb(0x15, 0x3A, 0x2A);
        var warningColor = Color.FromRgb(0xF0, 0xB4, 0x42);
        var warningSoftColor = isLight ? Color.FromRgb(0xFC, 0xF4, 0xDF) : Color.FromRgb(0x3B, 0x2B, 0x12);
        var errorColor = Color.FromRgb(0xF0, 0x44, 0x44);
        var errorHoverColor = Color.FromRgb(0xD9, 0x36, 0x36);
        var errorSoftColor = isLight ? Color.FromRgb(0xFE, 0xEA, 0xEA) : Color.FromRgb(0x46, 0x1D, 0x1D);
        var dialogCardColor = notificationSurfaceColor;
        var dialogHeaderColor = isLight ? Color.FromRgb(0xF1, 0xF5, 0xF8) : Color.FromRgb(0x24, 0x2B, 0x32);
        var dialogBorderColor = notificationBorderColor;
        var dialogMutedColor = isLight ? Color.FromRgb(0x5E, 0x6A, 0x75) : Color.FromRgb(0xB5, 0xC0, 0xC9);
        var dialogTextColor = notificationTitleColor;
        var dialogAccentColor = infoColor;
        var dialogBadgeBackgroundColor = isLight ? Color.FromRgb(0xEA, 0xF3, 0xFF) : Color.FromRgb(0x18, 0x2C, 0x3D);
        var dialogBadgeBorderColor = isLight ? Color.FromRgb(0xC8, 0xDB, 0xF8) : Color.FromRgb(0x31, 0x5A, 0x77);
        var app = Application.Current;
        if (app?.Resources == null) return;
        app.Resources["EmptyStateTitleBrush"] = new SolidColorBrush(emptyStateTitleColor);
        app.Resources["SystemControlBackgroundChromeMediumLowBrush"] = new SolidColorBrush(sidebarColor);
        app.Resources["ApplicationBackgroundBrush"] = new SolidColorBrush(bgColor);
        app.Resources["WindowBackground"] = new SolidColorBrush(bgColor);
        app.Resources["WindowBackgroundColor"] = bgColor;
        app.Resources["ShellSidebarBrush"] = new SolidColorBrush(sidebarColor);
        app.Resources["ShellSidebarBorderBrush"] = new SolidColorBrush(sidebarBorderColor);
        app.Resources["ShellContentBrush"] = new SolidColorBrush(bgColor);
        app.Resources["ShellTopBarBrush"] = new SolidColorBrush(bgColor);
        app.Resources["SurfaceBrush"] = new SolidColorBrush(surfaceColor);
        app.Resources["SurfaceRaisedBrush"] = new SolidColorBrush(surfaceRaisedColor);
        app.Resources["SurfaceHoverBrush"] = new SolidColorBrush(surfaceHoverColor);
        app.Resources["SurfacePressedBrush"] = new SolidColorBrush(surfacePressedColor);
        app.Resources["SubtleSurfaceBrush"] = new SolidColorBrush(subtleSurfaceColor);
        app.Resources["StrongTextBrush"] = new SolidColorBrush(strongTextColor);
        app.Resources["MutedTextBrush"] = new SolidColorBrush(secondaryTextColor);
        app.Resources["FaintTextBrush"] = new SolidColorBrush(faintTextColor);
        app.Resources["AccentPrimaryBrush"] = new SolidColorBrush(infoColor);
        app.Resources["AccentPrimaryHoverBrush"] = new SolidColorBrush(infoHoverColor);
        app.Resources["AccentPrimaryPressedBrush"] = new SolidColorBrush(infoPressedColor);
        app.Resources["PrimaryButtonBrush"] = new SolidColorBrush(primaryButtonColor);
        app.Resources["PrimaryButtonHoverBrush"] = new SolidColorBrush(primaryButtonHoverColor);
        app.Resources["PrimaryButtonPressedBrush"] = new SolidColorBrush(primaryButtonPressedColor);
        app.Resources["AccentSoftBrush"] = new SolidColorBrush(isLight ? Color.FromRgb(0xEA, 0xF3, 0xFF) : Color.FromRgb(0x18, 0x3B, 0x56));
        app.Resources["AccentSuccessBrush"] = new SolidColorBrush(successColor);
        app.Resources["AccentSuccessSoftBrush"] = new SolidColorBrush(successSoftColor);
        app.Resources["AccentWarningBrush"] = new SolidColorBrush(warningColor);
        app.Resources["AccentWarningSoftBrush"] = new SolidColorBrush(warningSoftColor);
        app.Resources["AccentDangerBrush"] = new SolidColorBrush(errorColor);
        app.Resources["AccentDangerHoverBrush"] = new SolidColorBrush(errorHoverColor);
        app.Resources["AccentDangerSoftBrush"] = new SolidColorBrush(errorSoftColor);
        app.Resources["DividerBrush"] = new SolidColorBrush(cardBorderColor);
        app.Resources["ElevatedShadowColor"] = elevatedShadowColor;
        app.Resources["CoverPlaceholderBrush"] = new SolidColorBrush(coverPlaceholderColor);
        app.Resources["SecondaryTextBrush"] = new SolidColorBrush(secondaryTextColor);
        app.Resources["CardBackgroundBrush"] = new SolidColorBrush(cardBgColor);
        app.Resources["CardBorderBrush"] = new SolidColorBrush(cardBorderColor);
        app.Resources["ListItemHoverBrush"] = new SolidColorBrush(listItemHoverColor);
        app.Resources["DialogCardBrush"] = new SolidColorBrush(dialogCardColor);
        app.Resources["DialogHeaderBrush"] = new SolidColorBrush(dialogHeaderColor);
        app.Resources["DialogBorderBrush"] = new SolidColorBrush(dialogBorderColor);
        app.Resources["DialogMutedBrush"] = new SolidColorBrush(dialogMutedColor);
        app.Resources["DialogTextBrush"] = new SolidColorBrush(dialogTextColor);
        app.Resources["DialogAccentBrush"] = new SolidColorBrush(dialogAccentColor);
        app.Resources["DialogBadgeBackgroundBrush"] = new SolidColorBrush(dialogBadgeBackgroundColor);
        app.Resources["DialogBadgeBorderBrush"] = new SolidColorBrush(dialogBadgeBorderColor);
        app.Resources["NotificationSurfaceBrush"] = new SolidColorBrush(notificationSurfaceColor);
        app.Resources["NotificationSurfaceBorderBrush"] = new SolidColorBrush(notificationBorderColor);
        app.Resources["NotificationTitleBrush"] = new SolidColorBrush(notificationTitleColor);
        app.Resources["NotificationMessageBrush"] = new SolidColorBrush(notificationMessageColor);
        app.Resources["NotificationCloseBrush"] = new SolidColorBrush(notificationCloseColor);
        app.Resources["NotificationShadowColor"] = notificationShadowColor;
        app.Resources["StatusInfoBrush"] = new SolidColorBrush(infoColor);
        app.Resources["StatusSuccessBrush"] = new SolidColorBrush(successColor);
        app.Resources["StatusWarningBrush"] = new SolidColorBrush(warningColor);
        app.Resources["StatusErrorBrush"] = new SolidColorBrush(errorColor);
        // 搜索按钮固定蓝底白字，不随主题变
        app.Resources["SearchButtonBackgroundBrush"] = new SolidColorBrush(primaryButtonColor);
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
