using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using NLog;

namespace GameTrainerLauncher.UI.Services;

public interface IShortcutRepairService
{
    void RepairInstalledShortcuts();
}

public sealed class ShortcutRepairService : IShortcutRepairService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private const string ShortcutFileName = "Game Trainer Launcher.lnk";

    public void RepairInstalledShortcuts()
    {
        try
        {
            var processPath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(processPath) || !File.Exists(processPath))
            {
                return;
            }

            var currentDir = Path.GetDirectoryName(processPath);
            if (string.IsNullOrWhiteSpace(currentDir))
            {
                return;
            }

            var installRoot = Directory.GetParent(currentDir)?.FullName;
            if (string.IsNullOrWhiteSpace(installRoot))
            {
                return;
            }

            var updateExePath = Path.Combine(installRoot, "Update.exe");
            var sqVersionPath = Path.Combine(currentDir, "sq.version");
            var runningFromCurrentDir = string.Equals(Path.GetFileName(currentDir), "current", StringComparison.OrdinalIgnoreCase);
            if (!runningFromCurrentDir || !File.Exists(updateExePath) || !File.Exists(sqVersionPath))
            {
                return;
            }

            var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var startMenuProgramsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.StartMenu), "Programs");

            RepairShortcut(Path.Combine(desktopPath, ShortcutFileName), processPath, currentDir);
            RepairShortcut(Path.Combine(startMenuProgramsPath, ShortcutFileName), processPath, currentDir);
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to repair application shortcuts.");
        }
    }

    private static void RepairShortcut(string shortcutPath, string targetPath, string workingDirectory)
    {
        try
        {
            var parentDir = Path.GetDirectoryName(shortcutPath);
            if (string.IsNullOrWhiteSpace(parentDir))
            {
                return;
            }

            Directory.CreateDirectory(parentDir);
            if (!ShortcutLinkWriter.NeedsRepair(shortcutPath, targetPath, workingDirectory))
            {
                return;
            }

            ShortcutLinkWriter.Write(shortcutPath, targetPath, workingDirectory);

            Logger.Info("Shortcut repaired: {ShortcutPath} -> {TargetPath}", shortcutPath, targetPath);
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to repair shortcut: {ShortcutPath}", shortcutPath);
        }
    }
}

internal static class ShortcutLinkWriter
{
    public static bool NeedsRepair(string shortcutPath, string targetPath, string workingDirectory)
    {
        var link = (IShellLinkW)new ShellLink();
        var persistFile = (IPersistFile)link;
        if (!File.Exists(shortcutPath))
        {
            return true;
        }

        persistFile.Load(shortcutPath, 0);
        var existingTarget = GetPath(link);
        var existingWorkDir = GetWorkingDirectory(link);
        return !string.Equals(existingTarget, targetPath, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(existingWorkDir, workingDirectory, StringComparison.OrdinalIgnoreCase);
    }

    public static void Write(string shortcutPath, string targetPath, string workingDirectory)
    {
        var link = (IShellLinkW)new ShellLink();
        link.SetPath(targetPath);
        link.SetWorkingDirectory(workingDirectory);
        link.SetArguments(string.Empty);
        link.SetIconLocation(targetPath, 0);

        var persistFile = (IPersistFile)link;
        persistFile.Save(shortcutPath, true);
    }

    private static string GetPath(IShellLinkW link)
    {
        var path = new StringBuilder(260);
        link.GetPath(path, path.Capacity, IntPtr.Zero, 0);
        return path.ToString();
    }

    private static string GetWorkingDirectory(IShellLinkW link)
    {
        var directory = new StringBuilder(260);
        link.GetWorkingDirectory(directory, directory.Capacity);
        return directory.ToString();
    }
}

[ComImport]
[Guid("00021401-0000-0000-C000-000000000046")]
internal class ShellLink
{
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("000214F9-0000-0000-C000-000000000046")]
internal interface IShellLinkW
{
    void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, int fFlags);
    void GetIDList(out IntPtr ppidl);
    void SetIDList(IntPtr pidl);
    void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
    void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
    void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
    void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
    void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
    void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
    void GetHotkey(out short pwHotkey);
    void SetHotkey(short wHotkey);
    void GetShowCmd(out int piShowCmd);
    void SetShowCmd(int iShowCmd);
    void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
    void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
    void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
    void Resolve(IntPtr hwnd, int fFlags);
    void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
}

[ComImport]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
[Guid("0000010b-0000-0000-C000-000000000046")]
internal interface IPersistFile
{
    void GetClassID(out Guid pClassID);
    void IsDirty();
    void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
    void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
    void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
    void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
}
