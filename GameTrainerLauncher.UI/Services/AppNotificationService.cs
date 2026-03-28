using System.Windows;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace GameTrainerLauncher.UI.Services;

public sealed class AppNotificationService : IAppNotificationService
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(4);

    private readonly ISnackbarService _snackbarService;

    public AppNotificationService(ISnackbarService snackbarService)
    {
        _snackbarService = snackbarService;
    }

    public void AttachPresenter(SnackbarPresenter presenter)
    {
        ArgumentNullException.ThrowIfNull(presenter);
        _snackbarService.SetSnackbarPresenter(presenter);
    }

    public void ShowSuccess(string message, string? title = null, TimeSpan? timeout = null)
    {
        Show(message, title ?? GetString("MsgSuccessTitle"), ControlAppearance.Success, timeout);
    }

    public void ShowInfo(string message, string? title = null, TimeSpan? timeout = null)
    {
        Show(message, title ?? GetString("MsgInfoTitle"), ControlAppearance.Info, timeout);
    }

    public void ShowWarning(string message, string? title = null, TimeSpan? timeout = null)
    {
        Show(message, title ?? GetString("MsgWarningTitle"), ControlAppearance.Caution, timeout);
    }

    public void ShowError(string message, string? title = null, TimeSpan? timeout = null)
    {
        Show(message, title ?? GetString("MsgErrorTitle"), ControlAppearance.Danger, timeout ?? TimeSpan.FromSeconds(6));
    }

    private void Show(string message, string title, ControlAppearance appearance, TimeSpan? timeout)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var dispatch = Application.Current?.Dispatcher;
        if (dispatch == null)
        {
            return;
        }

        _ = dispatch.InvokeAsync(() =>
        {
            _snackbarService.Show(title, message, appearance, timeout ?? DefaultTimeout);
        });
    }

    private static string GetString(string key)
    {
        return Application.Current.FindResource(key) as string ?? key;
    }
}
