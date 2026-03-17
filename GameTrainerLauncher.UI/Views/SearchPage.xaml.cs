using GameTrainerLauncher.UI.ViewModels;
using System.Windows.Controls;

namespace GameTrainerLauncher.UI.Views;

public partial class SearchPage : Page
{
    public SearchPage(SearchViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += (_, _) => { _ = viewModel.RefreshAlreadyInLibraryCommand.ExecuteAsync(null); };
    }
}