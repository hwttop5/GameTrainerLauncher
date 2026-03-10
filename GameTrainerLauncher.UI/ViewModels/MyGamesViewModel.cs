using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Interfaces;
using GameTrainerLauncher.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace GameTrainerLauncher.UI.ViewModels;

public partial class MyGamesViewModel : ObservableObject
{
    private readonly AppDbContext _dbContext;
    private readonly ITrainerManager _trainerManager;
    private readonly IEnumerable<IGameScanner> _scanners;
    private readonly IScraperService _scraperService;

    [ObservableProperty]
    private ObservableCollection<Game> _games = new();

    public MyGamesViewModel(AppDbContext dbContext, ITrainerManager trainerManager, IEnumerable<IGameScanner> scanners, IScraperService scraperService)
    {
        _dbContext = dbContext;
        _trainerManager = trainerManager;
        _scanners = scanners;
        _scraperService = scraperService;
        LoadGamesCommand.ExecuteAsync(null);
    }

    [RelayCommand]
    public async Task LoadGamesAsync()
    {
        // Ensure DB created
        try 
        {
            await _dbContext.Database.EnsureCreatedAsync();

            var dbGames = await _dbContext.Games.Include(g => g.MatchedTrainer).ToListAsync();
            
            // De-duplicate logic (though DB primary key handles ID, we want to ensure uniqueness by name for display)
            // Rebuild the observable collection
            Games.Clear();
            var uniqueGames = dbGames.GroupBy(g => g.Name).Select(g => g.First());
            foreach (var g in uniqueGames) Games.Add(g);

            // Scan logic - improved logging
            var scanReport = new System.Text.StringBuilder();
            bool hasErrors = false;

            foreach (var scanner in _scanners)
            {
                try
                {
                    System.Diagnostics.Debug.WriteLine($"Starting scan for {scanner.PlatformName}");
                    scanReport.AppendLine($"Scanning {scanner.PlatformName}...");
                    
                    var scanned = await scanner.ScanAsync();
                    System.Diagnostics.Debug.WriteLine($"Scanned {scanned.Count} games from {scanner.PlatformName}");
                    scanReport.AppendLine($"  Found {scanned.Count} games.");
                    
                    foreach (var g in scanned)
                    {
                        if (!Games.Any(x => x.Name == g.Name))
                        {
                            // Try to fetch cover image if missing
                            if (string.IsNullOrEmpty(g.CoverUrl))
                            {
                                try 
                                {
                                    var searchResults = await _scraperService.SearchAsync(g.Name);
                                    var bestMatch = searchResults.FirstOrDefault();
                                    if (bestMatch != null)
                                    {
                                        g.CoverUrl = bestMatch.ImageUrl;
                                        // Also link trainer if available
                                        if (g.MatchedTrainer == null)
                                        {
                                            // We don't fetch full details here to save time, 
                                            // but we could set a basic Trainer object or let user trigger download later
                                            // Ideally, DownloadTrainerAsync handles the search again.
                                        }
                                    }
                                }
                                catch 
                                {
                                    // Ignore image fetch errors during scan to avoid blocking
                                }
                            }
                            
                            Games.Add(g);
                            _dbContext.Games.Add(g);
                        }
                    }
                }
                catch (Exception ex)
                {
                    hasErrors = true;
                    System.Diagnostics.Debug.WriteLine($"Scanner error ({scanner.PlatformName}): {ex}");
                    scanReport.AppendLine($"  Error: {ex.Message}");
                }
            }
            await _dbContext.SaveChangesAsync();
            
            if (Games.Count == 0 && hasErrors)
            {
                var msg = GetString("MsgScanReportBody", scanReport.ToString());
                var title = GetString("MsgScanReportTitle");
                System.Windows.MessageBox.Show(msg, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }
        catch (Exception ex)
        {
             var title = GetString("MsgErrorTitle");
             var msg = GetString("MsgDatabaseError") + " " + ex.Message;
             System.Windows.MessageBox.Show(msg, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
    }

    [RelayCommand]
    public async Task LaunchTrainerAsync(Game game)
    {
        if (game.MatchedTrainer == null)
        {
             var msg = GetString("MsgNoTrainerFound");
             var title = GetString("MsgErrorTitle");
             System.Windows.MessageBox.Show(msg, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
             return;
        }

        // 3s timeout logic
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            game.MatchedTrainer.IsLoading = true;
            await Task.Run(async () => 
            {
                await _trainerManager.LaunchTrainerAsync(game.MatchedTrainer);
            }, cts.Token);
        }
        catch (OperationCanceledException)
        {
             var msg = GetString("MsgLaunchTimeout");
             var title = GetString("MsgTimeoutTitle");
             System.Windows.MessageBox.Show(msg, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
             var msg = GetString("MsgLaunchFailed") + " " + ex.Message;
             var title = GetString("MsgErrorTitle");
             System.Windows.MessageBox.Show(msg, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        finally
        {
            game.MatchedTrainer.IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task RemoveGameAsync(Game game)
    {
        var msg = GetString("MsgConfirmRemoveBody", game.Name);
        var title = GetString("MsgConfirmRemoveTitle");
        var result = System.Windows.MessageBox.Show(msg, title, System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
        if (result == System.Windows.MessageBoxResult.Yes)
        {
            try
            {
                Games.Remove(game);
                _dbContext.Games.Remove(game);
                await _dbContext.SaveChangesAsync();
                
                // Notify PopularGamesViewModel to update button state
                // In a real MVVM app, use Messenger/EventAggregator.
                // Here we can try to find the view model if registered as singleton, or rely on shared state.
                // Since PopularGamesViewModel reloads or checks DB, we might need to trigger a refresh.
                // However, PopularGamesViewModel loads data into memory.
                
                // For now, let's just save. If the user goes back to PopularGames, it might need to refresh.
                // But PopularGamesViewModel has IsDownloaded property on Trainer objects.
                // We need to find the trainer in PopularGamesViewModel and set IsDownloaded = false.
                
                // A quick hack for this simple architecture:
                // We can't easily reach other view models without a Messenger.
                // Let's assume the user will refresh or the app will reload.
                // But to satisfy the requirement "need to sync update", we should try.
                
                // If PopularGamesViewModel is Singleton (it is in App.xaml.cs), we can get it.
                var popularVM = (System.Windows.Application.Current as App)?.Services.GetService(typeof(PopularGamesViewModel)) as PopularGamesViewModel;
                if (popularVM != null)
                {
                    var trainer = popularVM.Trainers.FirstOrDefault(t => t.Title == game.Name); // Assuming Name matches Title
                    if (trainer != null)
                    {
                        trainer.IsDownloaded = false;
                    }
                }
            }
            catch (Exception ex)
            {
                 var removeMsg = GetString("MsgRemoveFailed") + " " + ex.Message;
                 var errTitle = GetString("MsgErrorTitle");
                 System.Windows.MessageBox.Show(removeMsg, errTitle, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
        }
    }

    private string GetString(string key, params object[] args)
    {
        var resource = System.Windows.Application.Current.FindResource(key) as string;
        if (resource == null) return key;
        return args.Length > 0 ? string.Format(resource, args) : resource;
    }

    [RelayCommand]
    public async Task DownloadTrainerAsync(Game game)
    {
        if (game.MatchedTrainer == null)
        {
             var results = await _scraperService.SearchAsync(game.Name);
             var match = results.FirstOrDefault();
             if (match != null)
             {
                 var details = await _scraperService.GetTrainerDetailsAsync(match.PageUrl);
                 game.MatchedTrainer = details;
                 _dbContext.Games.Update(game);
                 await _dbContext.SaveChangesAsync();
             }
             else
             {
                 var msg = GetString("MsgTrainerNotFound", game.Name);
                 var title = GetString("MsgErrorTitle");
                 System.Windows.MessageBox.Show(msg, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                 return; 
             }
        }

        if (game.MatchedTrainer != null)
        {
            var trainer = game.MatchedTrainer;
            trainer.IsDownloading = true;
            trainer.DownloadProgress = 0;

            var progress = new Progress<double>(p => 
            {
                trainer.DownloadProgress = p;
            });
            
            try
            {
                var success = await _trainerManager.DownloadTrainerAsync(trainer, progress);
                if (success)
                {
                    trainer.IsDownloaded = true;
                    _dbContext.Games.Update(game);
                    await _dbContext.SaveChangesAsync();
                }
                else
                {
                    var msg = GetString("MsgDownloadFailed");
                    var title = GetString("MsgErrorTitle");
                    System.Windows.MessageBox.Show(msg, title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                var title = GetString("MsgErrorTitle");
                System.Windows.MessageBox.Show(GetString("MsgErrorWithDetail", ex.Message), title, System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            }
            finally
            {
                trainer.IsDownloading = false;
            }
        }
    }
}
