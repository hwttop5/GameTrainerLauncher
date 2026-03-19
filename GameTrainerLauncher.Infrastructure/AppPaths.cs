using System;
using System.IO;

namespace GameTrainerLauncher.Infrastructure;

/// <summary>
/// 应用数据目录：使用 LocalApplicationData，避免安装到 Program Files 时无写权限。
/// </summary>
public static class AppPaths
{
    private static string? _appDataRoot;
    private static string? _dataFolder;
    private static string? _coversFolder;

    /// <summary>应用数据根目录，例如 %LocalAppData%\GameTrainerLauncher</summary>
    public static string AppDataRoot =>
        _appDataRoot ??= Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GameTrainerLauncher");

    /// <summary>Data 目录（数据库、Logs、Trainers、settings 等），例如 %LocalAppData%\GameTrainerLauncher\Data</summary>
    public static string DataFolder =>
        _dataFolder ??= Path.Combine(AppDataRoot, "Data");

    /// <summary>封面目录，例如 %LocalAppData%\GameTrainerLauncher\Data\Covers</summary>
    public static string CoversFolder =>
        _coversFolder ??= Path.Combine(DataFolder, "Covers");

    /// <summary>确保 Data 目录存在。</summary>
    public static void EnsureDataFolderExists()
    {
        try { Directory.CreateDirectory(DataFolder); } catch { }
    }

    /// <summary>确保 Covers 目录存在。</summary>
    public static void EnsureCoversFolderExists()
    {
        try
        {
            EnsureDataFolderExists();
            Directory.CreateDirectory(CoversFolder);
        }
        catch { }
    }
}
