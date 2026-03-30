using System.Windows.Interop;
using System.Windows.Media.Imaging;
using GameTrainerLauncher.UI.Services;
using GameTrainerLauncher.UI.ViewModels;
using Wpf.Ui.Controls;
using System.Windows;
using GameTrainerLauncher.UI.Models;

namespace GameTrainerLauncher.UI.Views;

public partial class MainWindow : FluentWindow
{
    private readonly IAppNotificationService _notificationService;
    private bool _hasAppliedStartupSize;

    public MainWindow(MainViewModel viewModel, INavigationService navigationService, IAppNotificationService notificationService)
    {
        InitializeComponent();
        _notificationService = notificationService;
        DataContext = viewModel;
        ((NavigationService)navigationService).Initialize(MainFrame);
        NotificationsItemsControl.ItemsSource = notificationService.Notifications;
        viewModel.NavigateTo("Popular");

        TrySetIconFromExecutable();

        Loaded += (_, _) => ApplyDefaultWindowSizeForCardGrid(columns: 4, rows: 2);
    }

    private void ApplyDefaultWindowSizeForCardGrid(int columns, int rows)
    {
        if (_hasAppliedStartupSize || columns <= 0 || rows <= 0 || WindowState != WindowState.Normal)
        {
            return;
        }

        // TrainerCard: Width=220 + Card.Margin(10,10) => 240
        const double cardOuterWidth = 240;
        // TrainerCard: MinHeight=284 + Card.Margin(10,10) => 304
        const double cardOuterHeight = 304;

        // SearchPage and MainWindow structural spacing.
        const double searchPageHorizontalPadding = 20; // SearchPage Grid Margin=10
        const double searchPageVerticalPadding = 20;   // SearchPage Grid Margin=10
        const double leftNavigationWidth = 200;        // MainWindow left column fixed width
        const double rightColumnMargin = 20;           // MainWindow Grid.Column=1 Margin=10
        const double titleBarHeight = 36;              // MainWindow row0 MinHeight=36
        const double searchBarRowHeight = 44;          // search input + button row estimated height
        const double searchBarBottomMargin = 8;        // row0 margin bottom
        const double frameSafetyWidth = 0;             // Scrollbar + border/pixel-rounding safety (max tightened)
        const double widthTightenOffset = 74;          // Additional startup width tighten (-20 more)

        var targetFrameWidth = columns * cardOuterWidth + searchPageHorizontalPadding + frameSafetyWidth;
        var targetFrameHeight = rows * cardOuterHeight + searchPageVerticalPadding;

        // Column(1) must include right grid margin, then row(1) must include search row and frame.
        var targetClientWidth = leftNavigationWidth + rightColumnMargin + targetFrameWidth;
        var targetClientHeight = titleBarHeight + rightColumnMargin + searchBarRowHeight + searchBarBottomMargin + targetFrameHeight;

        // Convert client-size target to window-size target.
        var chromeWidth = Math.Max(0, ActualWidth - RenderSize.Width);
        var chromeHeight = Math.Max(0, ActualHeight - RenderSize.Height);
        if (chromeWidth <= 0) chromeWidth = 16;
        if (chromeHeight <= 0) chromeHeight = 40;

        var targetWindowWidth = targetClientWidth + chromeWidth - widthTightenOffset;
        var targetWindowHeight = targetClientHeight + chromeHeight;

        // Keep within work area and retain minimum constraints.
        var workArea = SystemParameters.WorkArea;

        Width = Math.Max(MinWidth, Math.Min(workArea.Width, targetWindowWidth));
        Height = Math.Max(MinHeight, Math.Min(workArea.Height, targetWindowHeight));

        Left = workArea.Left + (workArea.Width - Width) / 2;
        Top = workArea.Top + (workArea.Height - Height) / 2;

        _hasAppliedStartupSize = true;
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

    private void NotificationClose_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is AppNotificationItem item)
        {
            _notificationService.Dismiss(item.Id);
        }
    }

}
