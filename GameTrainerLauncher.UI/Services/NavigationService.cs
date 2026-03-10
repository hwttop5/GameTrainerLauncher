using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;

namespace GameTrainerLauncher.UI.Services;

public class NavigationService : INavigationService
{
    private Frame? _frame;
    public event EventHandler<string>? Navigated;

    public void Initialize(Frame frame)
    {
        _frame = frame;
    }

    public void NavigateTo(string key)
    {
        if (_frame == null) return;
        
        if (key == "Popular") _frame.Navigate(App.Current.Services.GetService(typeof(Views.PopularGamesPage)));
        if (key == "MyGames") _frame.Navigate(App.Current.Services.GetService(typeof(Views.MyGamesPage)));
        if (key == "Search") _frame.Navigate(App.Current.Services.GetService(typeof(Views.SearchPage)));
        if (key == "Settings") _frame.Navigate(App.Current.Services.GetService(typeof(Views.SettingsPage)));
        
        Navigated?.Invoke(this, key);
    }
}
