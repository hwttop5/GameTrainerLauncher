using System.Windows;
using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Interfaces;
using GameTrainerLauncher.Core.Utilities;
using GameTrainerLauncher.UI.Views;

namespace GameTrainerLauncher.UI.Services;

public class TrainerVersionSelectionService : ITrainerVersionSelectionService
{
    private readonly IScraperService _scraperService;

    public TrainerVersionSelectionService(IScraperService scraperService)
    {
        _scraperService = scraperService;
    }

    public async Task<bool> EnsureSelectionAsync(Trainer trainer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trainer);

        if (trainer.DownloadOptions.Count == 0 && !string.IsNullOrWhiteSpace(trainer.PageUrl))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var details = await _scraperService.GetTrainerDetailsAsync(trainer.PageUrl);
            MergeDetails(trainer, details);
        }

        if (trainer.DownloadOptions.Count == 0 && !string.IsNullOrWhiteSpace(trainer.DownloadUrl))
        {
            trainer.DownloadOptions = [TrainerSelectionHelpers.CreateFallbackOption(trainer)];
        }

        if (trainer.DownloadOptions.Count == 0)
        {
            MessageBox.Show(
                GetString("MsgNoTrainerVersions"),
                GetString("MsgInfoTitle"),
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return false;
        }

        if (trainer.DownloadOptions.Count == 1)
        {
            TrainerSelectionHelpers.ApplyDownloadOption(trainer, trainer.DownloadOptions[0]);
            return true;
        }

        var pickerWindow = new TrainerVersionPickerWindow(trainer.Title, trainer.DownloadOptions, trainer.Version);
        var owner = Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(window => window.IsActive)
            ?? Application.Current.MainWindow;
        if (owner != null && !ReferenceEquals(owner, pickerWindow))
        {
            pickerWindow.Owner = owner;
        }

        var confirmed = pickerWindow.ShowDialog() == true;
        if (!confirmed || pickerWindow.SelectedOption == null)
        {
            return false;
        }

        TrainerSelectionHelpers.ApplyDownloadOption(trainer, pickerWindow.SelectedOption);
        return true;
    }

    private static void MergeDetails(Trainer target, Trainer details)
    {
        if (!string.IsNullOrWhiteSpace(details.ImageUrl))
        {
            target.ImageUrl = details.ImageUrl;
        }

        if (details.LastUpdated != null)
        {
            target.LastUpdated = details.LastUpdated;
        }

        target.DownloadOptions = details.DownloadOptions
            .OrderBy(option => option.SortOrder)
            .ToList();

        if (!string.IsNullOrWhiteSpace(details.DownloadUrl))
        {
            target.DownloadUrl = details.DownloadUrl;
        }

        if (!string.IsNullOrWhiteSpace(details.Version))
        {
            target.Version = details.Version;
        }
    }

    private static string GetString(string key)
    {
        return Application.Current.FindResource(key) as string ?? key;
    }
}
