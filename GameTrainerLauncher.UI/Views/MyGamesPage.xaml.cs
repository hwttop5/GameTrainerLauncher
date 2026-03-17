using GameTrainerLauncher.UI.Services;
using GameTrainerLauncher.UI.ViewModels;
using System.Windows.Controls;

namespace GameTrainerLauncher.UI.Views;

public partial class MyGamesPage : Page
{
    private readonly IMyGamesRefreshService _refreshService;

    public MyGamesPage(MyGamesViewModel viewModel, IMyGamesRefreshService refreshService)
    {
        InitializeComponent();
        DataContext = viewModel;
        _refreshService = refreshService;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
    {
        _refreshService.Register(() =>
        {
            if (DataContext is MyGamesViewModel vm)
                _ = vm.LoadGamesCommand.ExecuteAsync(null);
        });
        // 每次进入「我的游戏」页面都重新加载并检查/拉取缺失封面
        if (DataContext is MyGamesViewModel vm)
            _ = vm.LoadGamesCommand.ExecuteAsync(null);
    }

    private void OnUnloaded(object sender, System.Windows.RoutedEventArgs e)
    {
        _refreshService.Unregister();
    }
}
