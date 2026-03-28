using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using GameTrainerLauncher.UI.Models;

namespace GameTrainerLauncher.UI.Services;

public sealed class AppNotificationService : IAppNotificationService
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(4);
    private readonly ObservableCollection<AppNotificationItem> _notifications = [];

    public AppNotificationService()
    {
        Notifications = new ReadOnlyObservableCollection<AppNotificationItem>(_notifications);
    }

    public ReadOnlyObservableCollection<AppNotificationItem> Notifications { get; }

    public void Dismiss(Guid id)
    {
        var dispatch = Application.Current?.Dispatcher;
        if (dispatch == null)
        {
            return;
        }

        _ = dispatch.InvokeAsync(() =>
        {
            var item = _notifications.FirstOrDefault(notification => notification.Id == id);
            if (item != null)
            {
                _notifications.Remove(item);
            }
        });
    }

    public void ShowSuccess(string message, string? title = null, TimeSpan? timeout = null)
    {
        Show(message, title ?? GetString("MsgSuccessTitle"), "✓", "StatusSuccessBrush", timeout);
    }

    public void ShowInfo(string message, string? title = null, TimeSpan? timeout = null)
    {
        Show(message, title ?? GetString("MsgInfoTitle"), "i", "StatusInfoBrush", timeout);
    }

    public void ShowWarning(string message, string? title = null, TimeSpan? timeout = null)
    {
        Show(message, title ?? GetString("MsgWarningTitle"), "!", "StatusWarningBrush", timeout ?? TimeSpan.FromSeconds(5));
    }

    public void ShowError(string message, string? title = null, TimeSpan? timeout = null)
    {
        Show(message, title ?? GetString("MsgErrorTitle"), "×", "StatusErrorBrush", timeout ?? TimeSpan.FromSeconds(6));
    }

    private void Show(string message, string title, string glyph, string accentBrushKey, TimeSpan? timeout)
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
            var item = new AppNotificationItem
            {
                Id = Guid.NewGuid(),
                Title = title,
                Message = message,
                IconGlyph = glyph,
                AccentBrush = GetBrush(accentBrushKey)
            };

            _notifications.Add(item);
            _ = RemoveAfterDelayAsync(item.Id, timeout ?? DefaultTimeout);
        });
    }

    private async Task RemoveAfterDelayAsync(Guid id, TimeSpan timeout)
    {
        await Task.Delay(timeout);
        Dismiss(id);
    }

    private static Brush GetBrush(string key)
    {
        return Application.Current.FindResource(key) as Brush ?? Brushes.DodgerBlue;
    }

    private static string GetString(string key)
    {
        return Application.Current.FindResource(key) as string ?? key;
    }
}
