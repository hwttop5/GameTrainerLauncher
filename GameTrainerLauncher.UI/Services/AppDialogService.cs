using System.Windows;
using GameTrainerLauncher.UI.Views;

namespace GameTrainerLauncher.UI.Services;

public sealed class AppDialogService : IAppDialogService
{
    public void ShowMessage(string title, string message, AppDialogSeverity severity)
    {
        var window = new AppMessageDialogWindow(
            title,
            message,
            severity,
            GetString("DialogButtonOk"),
            null,
            primaryIsDanger: severity == AppDialogSeverity.Error);

        ShowWithOwner(window);
    }

    public bool ShowConfirmation(string title, string message, AppDialogSeverity severity, string? primaryButtonText = null, string? secondaryButtonText = null, bool primaryIsDanger = false)
    {
        var window = new AppMessageDialogWindow(
            title,
            message,
            severity,
            primaryButtonText ?? GetString("DialogButtonYes"),
            secondaryButtonText ?? GetString("DialogButtonNo"),
            primaryIsDanger);

        ShowWithOwner(window);
        return window.PrimaryAccepted;
    }

    private static void ShowWithOwner(Window window)
    {
        var owner = Application.Current.Windows
            .OfType<Window>()
            .FirstOrDefault(candidate => candidate.IsActive)
            ?? Application.Current.MainWindow;

        if (owner != null && !ReferenceEquals(owner, window))
        {
            window.Owner = owner;
        }

        window.ShowDialog();
    }

    private static string GetString(string key)
    {
        return Application.Current.FindResource(key) as string ?? key;
    }
}
