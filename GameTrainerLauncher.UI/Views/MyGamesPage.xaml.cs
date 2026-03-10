using GameTrainerLauncher.UI.ViewModels;
using System.Windows.Controls;

namespace GameTrainerLauncher.UI.Views;

public partial class MyGamesPage : Page
{
    public MyGamesPage(MyGamesViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
