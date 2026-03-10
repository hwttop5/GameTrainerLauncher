using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Interfaces;
using NLog;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace GameTrainerLauncher.Infrastructure.Services.Scanners;

public class XboxScanner : IGameScanner
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    public string PlatformName => "Xbox";

    public async Task<List<Game>> ScanAsync()
    {
        return await Task.Run(() =>
        {
            var games = new List<Game>();
            try
            {
                // Use winget to list installed packages from msstore
                var processStartInfo = new ProcessStartInfo
                {
                    FileName = "winget",
                    Arguments = "list --source msstore --accept-source-agreements",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                };

                using var process = Process.Start(processStartInfo);
                if (process == null) return games;

                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                // Parse winget output
                // Format: Name  Id  Version
                var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                
                // Skip header lines (usually first 2 lines)
                foreach (var line in lines.Skip(2))
                {
                    // Simple parsing logic - winget output is fixed width but width varies
                    // We'll assume the Id column looks like a package ID (contains dots)
                    // and Name is everything before it.
                    
                    // Regex to find the Package ID (e.g. "Microsoft.MinecraftUWP_8wekyb3d8bbwe")
                    // This is a heuristic and might need adjustment based on locale
                    var match = Regex.Match(line, @"^(.*?)\s+([a-zA-Z0-9\.]+_8wekyb3d8bbwe)\s+");
                    
                    if (match.Success)
                    {
                        var name = match.Groups[1].Value.Trim();
                        var id = match.Groups[2].Value.Trim();

                        // Filter out system components if possible, or keep everything
                        if (!string.IsNullOrEmpty(name))
                        {
                            games.Add(new Game
                            {
                                Name = name,
                                AppId = id,
                                Platform = "Xbox",
                                AddedDate = DateTime.Now
                                // InstallPath is hard to get via winget, usually in WindowsApps which is restricted
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Xbox scan failed (winget)");
            }
            return games;
        });
    }
}
