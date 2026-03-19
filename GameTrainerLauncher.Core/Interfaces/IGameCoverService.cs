namespace GameTrainerLauncher.Core.Interfaces;

public interface IGameCoverService
{
    /// <summary>返回该游戏封面本地路径（若不存在则返回预期路径）。</summary>
    string GetCoverFilePath(int gameId, string? coverUrl = null);

    /// <summary>检查本地是否已有封面文件。</summary>
    bool HasCover(int gameId);

    /// <summary>确保本地存在封面：若不存在则从 coverUrl 下载保存。</summary>
    Task<bool> EnsureCoverAsync(int gameId, string? coverUrl, CancellationToken cancellationToken = default);

    /// <summary>删除该游戏封面的本地文件（若存在）。</summary>
    void DeleteCover(int gameId);
}

