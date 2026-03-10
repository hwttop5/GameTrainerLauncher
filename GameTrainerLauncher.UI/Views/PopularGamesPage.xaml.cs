using GameTrainerLauncher.UI.ViewModels;
using System.Windows.Controls;

namespace GameTrainerLauncher.UI.Views;

public partial class PopularGamesPage : Page
{
    public PopularGamesPage(PopularGamesViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
