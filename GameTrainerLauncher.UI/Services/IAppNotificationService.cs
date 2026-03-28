using Wpf.Ui.Controls;

namespace GameTrainerLauncher.UI.Services;

public interface IAppNotificationService
{
    void AttachPresenter(SnackbarPresenter presenter);
    void ShowSuccess(string message, string? title = null, TimeSpan? timeout = null);
    void ShowInfo(string message, string? title = null, TimeSpan? timeout = null);
    void ShowWarning(string message, string? title = null, TimeSpan? timeout = null);
    void ShowError(string message, string? title = null, TimeSpan? timeout = null);
}
