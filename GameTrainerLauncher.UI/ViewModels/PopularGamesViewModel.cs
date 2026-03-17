using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Interfaces;
using GameTrainerLauncher.Infrastructure.Data;
using System.Collections.ObjectModel;

namespace GameTrainerLauncher.UI.ViewModels;

public partial class PopularGamesViewModel : ObservableObject
{
    private readonly IScraperService _scraperService;
    private readonly AppDbContext _dbContext;
    private int _currentPage = 1;

    [ObservableProperty]
    private ObservableCollection<Trainer> _trainers = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoadMoreVisible))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLoadMoreVisible))]
    private bool _canLoadMore = true;

    public bool IsLoadMoreVisible => CanLoadMore && !IsLoading;

    public PopularGamesViewModel(IScraperService scraperService, AppDbContext dbContext)
    {
        _scraperService = scraperService;
        _dbContext = dbContext;
        LoadDataCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        if (IsLoading) return;
        IsLoading = true;
        try
        {
            // Reset page
            _currentPage = 1;
            var data = await _scraperService.GetPopularTrainersAsync(_currentPage);
            Trainers.Clear();
            
            // Get existing game names to check for duplicates/added status
            await _dbContext.Database.EnsureCreatedAsync();
            var existingNames = _dbContext.Games.Select(g => g.Name).ToHashSet();

            foreach (var t in data) 
            {
                // Simple check: if game name exists, mark as added (UI can disable button)
                // Note: Trainer entity might need an IsAdded property for UI binding
                if (existingNames.Contains(t.Title))
                {
                    t.IsDownloaded = true; // Reusing this flag for "Added" status in UI for now, or add a new property
                }
                Trainers.Add(t);
            }
            CanLoadMore = data.Count > 0;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task LoadMoreAsync()
    {
        if (IsLoading || !CanLoadMore) return;
        IsLoading = true;
        try
        {
            _currentPage++;
            var data = await _scraperService.GetPopularTrainersAsync(_currentPage);
            
            if (data.Count == 0)
            {
                CanLoadMore = false;
            }
            else
            {
                await _dbContext.Database.EnsureCreatedAsync();
                var existingNames = _dbContext.Games.Select(g => g.Name).ToHashSet();
                foreach (var t in data)
                {
                    if (existingNames.Contains(t.Title))
                    {
                        t.IsDownloaded = true; 
                    }
                    Trainers.Add(t);
                }
            }
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

            // Check if already exists
            if (_dbContext.Games.Any(g => g.Name == trainer.Title))
            {
                 var msg = (string)System.Windows.Application.Current.FindResource("MsgAlreadyInLibrary");
                 var title = (string)System.Windows.Application.Current.FindResource("MsgInfoTitle");
                 System.Windows.MessageBox.Show(msg, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                 return;
            }

            // Each game gets its own Trainer row so download state is per-game (no shared trainer)
            var newTrainer = new Trainer
            {
                Title = trainer.Title,
                PageUrl = trainer.PageUrl,
                DownloadUrl = trainer.DownloadUrl,
                ImageUrl = trainer.ImageUrl,
                LastUpdated = trainer.LastUpdated,
                IsDownloaded = false
            };
            var game = new Game
            {
                Name = trainer.Title,
                MatchedTrainer = newTrainer,
                AddedDate = DateTime.Now,
                CoverUrl = trainer.ImageUrl
            };

            _dbContext.Games.Add(game);
            await _dbContext.SaveChangesAsync();
            
            trainer.IsDownloaded = true; // Update UI on the list card
            
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
