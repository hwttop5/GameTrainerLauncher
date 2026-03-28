namespace GameTrainerLauncher.UI.Services;

public enum AppDialogSeverity
{
    Information,
    Success,
    Warning,
    Error
}

public interface IAppDialogService
{
    void ShowMessage(string title, string message, AppDialogSeverity severity);
    bool ShowConfirmation(string title, string message, AppDialogSeverity severity, string? primaryButtonText = null, string? secondaryButtonText = null, bool primaryIsDanger = false);
}
