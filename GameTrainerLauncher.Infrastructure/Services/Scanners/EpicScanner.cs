using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Interfaces;
using Newtonsoft.Json.Linq;
using NLog;
using System.IO;

namespace GameTrainerLauncher.Infrastructure.Services.Scanners;

public class EpicScanner : IGameScanner
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    public string PlatformName => "Epic";

    public async Task<List<Game>> ScanAsync()
    {
        return await Task.Run(async () =>
        {
            var games = new List<Game>();
            var manifestPath = @"C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests";
            Logger.Info($"Scanning Epic manifests at: {manifestPath}");

            if (!Directory.Exists(manifestPath))
            {
                Logger.Warn($"Epic manifest directory not found: {manifestPath}");
                return games;
            }

            try
            {
                var files = Directory.GetFiles(manifestPath, "*.item");
                Logger.Info($"Found {files.Length} Epic manifest files.");
                
                foreach (var file in files)
                {
                    try
                    {
                        var content = await File.ReadAllTextAsync(file);
                        var json = JObject.Parse(content);
                        
                        var name = json["DisplayName"]?.ToString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            games.Add(new Game
                            {
                                Name = name,
                                AppId = json["CatalogItemId"]?.ToString(),
                                InstallPath = json["InstallLocation"]?.ToString(),
                                Platform = "Epic",
                                AddedDate = DateTime.Now
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                         Logger.Warn(ex, $"Failed to parse Epic manifest: {file}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Epic scan failed");
            }
            return games;
        });
    }
}
