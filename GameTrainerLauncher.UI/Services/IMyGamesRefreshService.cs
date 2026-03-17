namespace GameTrainerLauncher.UI.Services;

/// <summary>
/// 用于在「添加到我的游戏」成功后通知「我的游戏」页面刷新列表。
/// </summary>
public interface IMyGamesRefreshService
{
    void Register(Action onRefresh);
    void Unregister();
    void RequestRefresh();
}
