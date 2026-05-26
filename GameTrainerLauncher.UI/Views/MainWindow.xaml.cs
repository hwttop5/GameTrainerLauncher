using System.Windows.Interop;
using System.Windows.Media.Imaging;
using GameTrainerLauncher.UI.Services;
using GameTrainerLauncher.UI.ViewModels;
using Wpf.Ui.Controls;
using System.IO;
using System.Text.Json;
using System.Windows.Media;

namespace GameTrainerLauncher.UI.Views;

public partial class MainWindow : FluentWindow
{
    public MainWindow(MainViewModel viewModel, INavigationService navigationService)
    {
        InitializeComponent();
        DataContext = viewModel;
        ((NavigationService)navigationService).Initialize(MainFrame);
        viewModel.NavigateTo("Popular");

        TrySetIconFromExecutable();

        // #region agent log
        Loaded += (_, _) =>
        {
            double? searchBorderOpacity = null;
            string? searchBorderType = null;
            string? searchBorderColor = null;
            double? searchForegroundOpacity = null;
            string? searchForegroundType = null;
            string? searchForegroundColor = null;
            try
            {
                if (SearchButtonBorder?.BorderBrush is Brush b)
                {
                    searchBorderOpacity = b.Opacity;
                    searchBorderType = b.GetType().Name;
                    if (b is SolidColorBrush scb)
                        searchBorderColor = scb.Color.ToString();
                }

                if (SearchButton?.Foreground is Brush f)
                {
                    searchForegroundOpacity = f.Opacity;
                    searchForegroundType = f.GetType().Name;
                    if (f is SolidColorBrush fscb)
                        searchForegroundColor = fscb.Color.ToString();
                }
            }
            catch { }

            TryWriteDebugLog("H_border", "MainWindow.xaml.cs:Loaded", "Capture border styles", new
            {
                navSettingsBorder = NavSettingsButton?.BorderBrush?.ToString(),
                navSettingsThickness = NavSettingsButton?.BorderThickness.ToString(),
                searchBorder = SearchButton?.BorderBrush?.ToString(),
                searchThickness = SearchButton?.BorderThickness.ToString(),
                searchBorderBrushType = searchBorderType,
                searchBorderBrushOpacity = searchBorderOpacity,
                searchBorderBrushColor = searchBorderColor
                ,
                searchForegroundBrushType = searchForegroundType,
                searchForegroundBrushOpacity = searchForegroundOpacity,
                searchForegroundBrushColor = searchForegroundColor
            });
        };
        // #endregion
    }

    private void TrySetIconFromExecutable()
    {
        try
        {
            var exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrWhiteSpace(exePath))
                return;

            using var icon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
            if (icon is null)
                return;

            Icon = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle,
                System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        catch
        {
            // 忽略：图标非关键，XAML 中已有 Icon="/Assets/logo.ico" 兜底
        }
    }

    // #region agent log
    private static void TryWriteDebugLog(string hypothesisId, string location, string message, object data)
    {
        try
        {
            var payload = new
            {
                sessionId = "d901ba",
                runId = "pre-fix",
                hypothesisId,
                location,
                message,
                data,
                timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            };

            File.AppendAllText(
                Path.Combine(System.Environment.CurrentDirectory, "debug-d901ba.log"),
                JsonSerializer.Serialize(payload) + System.Environment.NewLine);
        }
        catch { }
    }
    // #endregion
}
