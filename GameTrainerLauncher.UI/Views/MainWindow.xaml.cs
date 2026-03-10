using Wpf.Ui.Controls;
using GameTrainerLauncher.UI.Services;
using GameTrainerLauncher.UI.ViewModels;
using System.Windows;

namespace GameTrainerLauncher.UI.Views;

public partial class MainWindow : FluentWindow
{
    public MainWindow(MainViewModel viewModel, INavigationService navigationService)
    {
        InitializeComponent();
        DataContext = viewModel;
        ((NavigationService)navigationService).Initialize(MainFrame);
        viewModel.NavigateTo("Popular");
    }
}
