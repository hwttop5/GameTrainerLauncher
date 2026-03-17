using System.Windows.Interop;
using System.Windows.Media.Imaging;
using GameTrainerLauncher.UI.Services;
using GameTrainerLauncher.UI.ViewModels;
using Wpf.Ui.Controls;

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
}
