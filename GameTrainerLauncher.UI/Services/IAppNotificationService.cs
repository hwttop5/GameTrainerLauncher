using System.Collections.ObjectModel;
using GameTrainerLauncher.UI.Models;

namespace GameTrainerLauncher.UI.Services;

public interface IAppNotificationService
{
    ReadOnlyObservableCollection<AppNotificationItem> Notifications { get; }
    void Dismiss(Guid id);
    void ShowSuccess(string message, string? title = null, TimeSpan? timeout = null);
    void ShowInfo(string message, string? title = null, TimeSpan? timeout = null);
    void ShowWarning(string message, string? title = null, TimeSpan? timeout = null);
    void ShowError(string message, string? title = null, TimeSpan? timeout = null);
}
