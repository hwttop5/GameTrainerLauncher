using System.Windows;
using System.Windows.Threading;

namespace GameTrainerLauncher.UI.Services;

public class MyGamesRefreshService : IMyGamesRefreshService
{
    private Action? _onRefresh;

    public void Register(Action onRefresh)
    {
        _onRefresh = onRefresh;
    }

    public void Unregister()
    {
        _onRefresh = null;
    }

    public void RequestRefresh()
    {
        var action = _onRefresh;
        if (action == null) return;
        var dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;
        dispatcher.InvokeAsync(() =>
        {
            try { _onRefresh?.Invoke(); } catch { /* ignore */ }
        });
    }
}
