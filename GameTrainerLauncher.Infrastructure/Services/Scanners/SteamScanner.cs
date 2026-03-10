using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Interfaces;
using Microsoft.Win32;
using System.IO;
using NLog;

namespace GameTrainerLauncher.Infrastructure.Services.Scanners;

public class SteamScanner : IGameScanner
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    public string PlatformName => "Steam";

    public async Task<List<Game>> ScanAsync()
    {
        return await Task.Run(() =>
        {
            var games = new List<Game>();
            try
            {
                Logger.Info("Starting Steam scan...");
                
                // 1. Find Steam Path (Check 32-bit and 64-bit keys)
                string? steamPath = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null) as string;
                if (string.IsNullOrEmpty(steamPath))
                {
                    steamPath = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null) as string;
                }
                if (string.IsNullOrEmpty(steamPath))
                {
                    steamPath = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Wow6432Node\Valve\Steam", "InstallPath", null) as string;
                }

                if (string.IsNullOrEmpty(steamPath)) 
                {
                    Logger.Warn("Steam path not found in registry.");
                    return games;
                }

                steamPath = steamPath.Replace("/", "\\");
                Logger.Info($"Steam path found: {steamPath}");
                
                // 2. Find Library Folders
                var libraryPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                libraryPaths.Add(Path.Combine(steamPath, "steamapps"));

                var libraryFoldersPath = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
                if (File.Exists(libraryFoldersPath))
                {
                    try 
                    {
                        var lines = File.ReadAllLines(libraryFoldersPath);
                        foreach (var line in lines)
                        {
                            // Robust VDF "path" parsing
                            // Format: "path" "C:\\Path\\To\\Lib"
                            if (line.Contains("\"path\"", StringComparison.OrdinalIgnoreCase))
                            {
                                var parts = line.Split('"');
                                // ["", "path", "whitespace", "VALUE", ""]
                                // We iterate to find "path" then take the next non-empty/whitespace part
                                for (int i = 0; i < parts.Length; i++)
                                {
                                    if (parts[i].Trim().Equals("path", StringComparison.OrdinalIgnoreCase))
                                    {
                                        // Find next valid string part
                                        for (int j = i + 1; j < parts.Length; j++)
                                        {
                                            if (!string.IsNullOrWhiteSpace(parts[j]))
                                            {
                                                var path = parts[j].Replace("\\\\", "\\");
                                                if (Directory.Exists(path))
                                                {
                                                    libraryPaths.Add(Path.Combine(path, "steamapps"));
                                                }
                                                break; // Found the path for this line
                                            }
                                        }
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(ex, "Error parsing libraryfolders.vdf");
                    }
                }
                else
                {
                    Logger.Warn($"libraryfolders.vdf not found at {libraryFoldersPath}");
                }

                Logger.Info($"Found {libraryPaths.Count} Steam libraries.");

                // 3. Scan Manifests
                foreach (var libPath in libraryPaths)
                {
                    if (Directory.Exists(libPath))
                    {
                        var manifests = Directory.GetFiles(libPath, "appmanifest_*.acf");
                        Logger.Info($"Scanning {manifests.Length} manifests in {libPath}");
                        
                        foreach (var manifest in manifests)
                        {
                            var game = ParseManifest(manifest, libPath);
                            if (game != null) games.Add(game);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Steam scan failed");
            }
            
            Logger.Info($"Steam scan completed. Found {games.Count} games.");
            return games;
        });
    }

    private Game? ParseManifest(string path, string libPath)
    {
        try
        {
            var lines = File.ReadAllLines(path);
            string? name = null;
            string? appId = null;
            string? installDir = null;

            foreach (var line in lines)
            {
                if (line.Contains("\"name\"") && name == null) name = ExtractValue(line);
                if (line.Contains("\"appid\"") && appId == null) appId = ExtractValue(line);
                if (line.Contains("\"installdir\"") && installDir == null) installDir = ExtractValue(line);
            }

            if (name != null && appId != null)
            {
                return new Game
                {
                    Name = name,
                    AppId = appId,
                    Platform = "Steam",
                    InstallPath = installDir != null ? Path.Combine(libPath, "common", installDir) : null,
                    AddedDate = DateTime.Now
                };
            }
        }
        catch (Exception ex) 
        {
             Logger.Warn(ex, $"Failed to parse manifest: {path}");
        }
        return null;
    }

    private string ExtractValue(string line)
    {
        var parts = line.Split('"');
        // Find the first part that is a key, then finding the next part that is a value
        // But here we assume we are passed a line that contains the key already checked by caller
        // e.g. "name" "Game Name"
        
        bool foundKey = false;
        for (int i = 0; i < parts.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(parts[i])) continue;
            
            if (!foundKey)
            {
                // This is the key (e.g. "name")
                foundKey = true;
            }
            else
            {
                // This is the value
                return parts[i];
            }
        }
        return "";
    }
}
