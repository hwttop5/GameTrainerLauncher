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

    public MainWindow(
        MainViewModel viewModel,
        INavigationService navigationService,
        IAppNotificationService notificationService)
    {
        InitializeComponent();
        _notificationService = notificationService;
        DataContext = viewModel;
        ((NavigationService)navigationService).Initialize(MainFrame);
        NotificationsItemsControl.ItemsSource = notificationService.Notifications;
        viewModel.NavigateTo("Popular");

        TrySetIconFromExecutable();
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
