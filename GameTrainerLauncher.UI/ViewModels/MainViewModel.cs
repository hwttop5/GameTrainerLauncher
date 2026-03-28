using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Interfaces;
using GameTrainerLauncher.UI.Services;
using System.Collections.ObjectModel;

namespace GameTrainerLauncher.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IScraperService _scraperService;
    private readonly INavigationService _navigationService;
    private readonly IServiceProvider _serviceProvider;
    private readonly IAppNotificationService _notificationService;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<Trainer> _searchResults = new();

    /// <summary>Current page key for left menu highlighting: Popular, MyGames, Search, Settings.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPopularSelected))]
    [NotifyPropertyChangedFor(nameof(IsMyGamesSelected))]
    [NotifyPropertyChangedFor(nameof(IsSearchSelected))]
    [NotifyPropertyChangedFor(nameof(IsSettingsSelected))]
    private string _currentPageKey = "Popular";

    public bool IsPopularSelected => CurrentPageKey == "Popular";
    public bool IsMyGamesSelected => CurrentPageKey == "MyGames";
    public bool IsSearchSelected => CurrentPageKey == "Search";
    public bool IsSettingsSelected => CurrentPageKey == "Settings";

    public MainViewModel(
        IScraperService scraperService,
        INavigationService navigationService,
        IServiceProvider serviceProvider,
        IAppNotificationService notificationService)
    {
        _scraperService = scraperService;
        _navigationService = navigationService;
        _serviceProvider = serviceProvider;
        _notificationService = notificationService;
        _navigationService.Navigated += (_, key) => CurrentPageKey = key;
    }

    [RelayCommand]
    public async Task SearchAsync()
    {
        var keyword = SearchText?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(keyword))
        {
            var msg = (string)System.Windows.Application.Current.FindResource("MsgSearchEmpty") ?? "Please enter a game name before searching.";
            var title = (string)System.Windows.Application.Current.FindResource("MsgSearchTitle") ?? "Search";
            _notificationService.ShowInfo(msg, title);
            return;
        }

        CurrentPageKey = "Search";
        _navigationService.NavigateTo("Search");

        var searchVM = _serviceProvider.GetService(typeof(SearchViewModel)) as SearchViewModel;
        if (searchVM != null)
        {
            await searchVM.SearchAsync(keyword);
        }
    }

    [RelayCommand]
    public void SelectSearchResult(Trainer trainer)
    {
        // No longer used as we disabled dropdown behavior logic in favor of full search page
    }

    [RelayCommand]
    public void NavigateTo(string pageKey)
    {
        if (string.IsNullOrEmpty(pageKey)) return;
        try
        {
            CurrentPageKey = pageKey;
            _navigationService.NavigateTo(pageKey);
        }
        catch (Exception ex)
        {
            var msg = string.Format((string)System.Windows.Application.Current.FindResource("MsgNavigationError") ?? "Navigation error: {0}", ex.Message);
            _notificationService.ShowError(msg);
        }
    }
}
