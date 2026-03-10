using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Interfaces;
using GameTrainerLauncher.Infrastructure.Data;
using System.Collections.ObjectModel;
using System.Linq;

namespace GameTrainerLauncher.UI.ViewModels;

public partial class SearchViewModel : ObservableObject
{
    private readonly IScraperService _scraperService;
    private readonly AppDbContext _dbContext;

    [ObservableProperty]
    private ObservableCollection<Trainer> _searchResults = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _searchKeyword = string.Empty;

    public SearchViewModel(IScraperService scraperService, AppDbContext dbContext)
    {
        _scraperService = scraperService;
        _dbContext = dbContext;
    }

    [ObservableProperty]
    private bool _hasNoResults;

    /// <summary>Run search using current SearchKeyword (for in-page search box).</summary>
    [RelayCommand]
    public async Task RunSearchAsync()
    {
        var keyword = (SearchKeyword ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(keyword))
        {
            var msg = (string)System.Windows.Application.Current.FindResource("MsgSearchEmpty") ?? "Please enter a game name before searching.";
            var title = (string)System.Windows.Application.Current.FindResource("MsgSearchTitle") ?? "Search";
            System.Windows.MessageBox.Show(msg, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            return;
        }
        await SearchAsync(keyword);
    }

    public async Task SearchAsync(string keyword)
    {
        SearchKeyword = keyword;
        IsLoading = true;
        HasNoResults = false;
        SearchResults.Clear();
        
        try
        {
            var data = await _scraperService.SearchAsync(keyword);
            
            await _dbContext.Database.EnsureCreatedAsync();
            var existingNames = _dbContext.Games.Select(g => g.Name).ToHashSet();

            foreach (var t in data) 
            {
                if (existingNames.Contains(t.Title))
                {
                    t.IsDownloaded = true;
                }
                SearchResults.Add(t);
            }

            if (SearchResults.Count == 0)
            {
                HasNoResults = true;
            }
        }
        catch (Exception ex)
        {
            var msg = ((string)System.Windows.Application.Current.FindResource("MsgSearchFailed") ?? "Search failed.") + " " + ex.Message;
            var title = (string)System.Windows.Application.Current.FindResource("MsgErrorTitle") ?? "Error";
            System.Windows.MessageBox.Show(msg, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task AddToMyGamesAsync(Trainer trainer)
    {
        try
        {
            await _dbContext.Database.EnsureCreatedAsync();

            if (_dbContext.Games.Any(g => g.Name == trainer.Title))
            {
                 var msg = (string)System.Windows.Application.Current.FindResource("MsgAlreadyInLibrary");
                 var title = (string)System.Windows.Application.Current.FindResource("MsgInfoTitle");
                 System.Windows.MessageBox.Show(msg, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                 return;
            }

            var game = new Game
            {
                Name = trainer.Title,
                MatchedTrainer = trainer,
                AddedDate = DateTime.Now,
                CoverUrl = trainer.ImageUrl
            };
            _dbContext.Games.Add(game);
            await _dbContext.SaveChangesAsync();
            
            trainer.IsDownloaded = true;
            
            var successMsg = (string)System.Windows.Application.Current.FindResource("MsgAddedToMyGames");
            var successTitle = (string)System.Windows.Application.Current.FindResource("MsgSuccessTitle");
            System.Windows.MessageBox.Show(successMsg, successTitle, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
             var msg = (string)System.Windows.Application.Current.FindResource("MsgAddFailed") + " " + ex.Message;
             var title = (string)System.Windows.Application.Current.FindResource("MsgErrorTitle");
             System.Windows.MessageBox.Show(msg, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }
}