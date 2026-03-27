using System.Windows;
using GameTrainerLauncher.UI.Views;

namespace GameTrainerLauncher.UI.Services;

internal static class AppUpdateFlow
{
    public static async Task HandleCheckResultAsync(Window? owner, IAppUpdateService updateService, UpdateCheckResult result, bool manual, CancellationToken cancellationToken = default)
    {
        switch (result.State)
        {
            case AppUpdateState.UpdateAvailable:
                await PromptAndApplyUpdateAsync(owner, updateService, result, cancellationToken);
                break;
            case AppUpdateState.UpToDate when manual:
            case AppUpdateState.PendingRestart when manual:
            case AppUpdateState.NotInstalled when manual:
            case AppUpdateState.Error when manual:
                MessageBox.Show(
                    UpdateTextFormatter.GetManualCheckMessage(result),
                    UpdateTextFormatter.GetString("UpdateDialogTitle"),
                    MessageBoxButton.OK,
                    result.State == AppUpdateState.Error ? MessageBoxImage.Error : MessageBoxImage.Information);
                break;
        }
    }

    private static async Task PromptAndApplyUpdateAsync(Window? owner, IAppUpdateService updateService, UpdateCheckResult result, CancellationToken cancellationToken)
    {
        var promptWindow = new UpdatePromptWindow(result);
        if (owner != null)
        {
            promptWindow.Owner = owner;
        }

        promptWindow.ShowDialog();
        switch (promptWindow.SelectedAction)
        {
            case UpdatePromptAction.SkipVersion when !string.IsNullOrWhiteSpace(result.AvailableVersion):
                updateService.SkipVersion(result.AvailableVersion);
                break;
            case UpdatePromptAction.UpdateNow:
                await DownloadAndRestartAsync(owner, updateService, result, cancellationToken);
                break;
        }
    }

    private static async Task DownloadAndRestartAsync(Window? owner, IAppUpdateService updateService, UpdateCheckResult result, CancellationToken cancellationToken)
    {
        var progressWindow = new UpdateProgressWindow();
        if (owner != null)
        {
            progressWindow.Owner = owner;
        }

        progressWindow.Show();

        try
        {
            var success = await updateService.DownloadUpdateAndRestartAsync(result, progress =>
            {
                progressWindow.Dispatcher.Invoke(() => progressWindow.SetProgress(progress));
            }, cancellationToken);

            if (!success)
            {
                progressWindow.AllowClose();
                progressWindow.Close();
                var status = updateService.GetStatusSnapshot();
                var message = UpdateTextFormatter.GetString("UpdateDownloadFailed");
                if (!string.IsNullOrWhiteSpace(status.ErrorMessage))
                {
                    message = $"{message}{Environment.NewLine}{Environment.NewLine}{status.ErrorMessage}";
                }

                MessageBox.Show(
                    message,
                    UpdateTextFormatter.GetString("UpdateDialogTitle"),
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                return;
            }

            progressWindow.SetMessage(UpdateTextFormatter.GetString("UpdateRestartingMessage"));
            progressWindow.AllowClose();
            progressWindow.Close();
            Application.Current.Shutdown();
        }
        catch (Exception)
        {
            progressWindow.AllowClose();
            progressWindow.Close();
            throw;
        }
    }
}
