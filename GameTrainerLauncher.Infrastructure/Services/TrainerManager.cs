using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Interfaces;
using NLog;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;

namespace GameTrainerLauncher.Infrastructure.Services;

public class TrainerManager : ITrainerManager
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly HttpClient _httpClient = new HttpClient();
    private const string BaseFolder = "GameTrainerLauncher/Trainers";

    public async Task<bool> DownloadTrainerAsync(Trainer trainer, IProgress<double> progress, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(trainer.DownloadUrl)) return false;

            var localAppData = AppDomain.CurrentDomain.BaseDirectory;
            var trainerFolder = Path.Combine(localAppData, "Data", "Trainers", trainer.Title.Replace(" ", "_"));
            Directory.CreateDirectory(trainerFolder);

            var zipPath = Path.Combine(trainerFolder, "trainer.zip");
            
            // Download Logic
            using (var response = await _httpClient.GetAsync(trainer.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var canReportProgress = totalBytes != -1;

                using (var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken))
                using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true))
                {
                    var buffer = new byte[8192];
                    long totalRead = 0;
                    int read;

                    while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
                        totalRead += read;

                        if (canReportProgress)
                        {
                            progress.Report((double)totalRead / totalBytes * 100);
                        }
                    }
                }
            }

            // Extraction Logic
            try 
            {
                ZipFile.ExtractToDirectory(zipPath, trainerFolder, true);
                trainer.LocalZipPath = zipPath;
                
                // Find EXE
                var exeFiles = Directory.GetFiles(trainerFolder, "*.exe");
                // Filter out non-trainers if possible
                var trainerExe = exeFiles.FirstOrDefault(f => !Path.GetFileName(f).StartsWith("unins", StringComparison.OrdinalIgnoreCase) && !Path.GetFileName(f).StartsWith("readme", StringComparison.OrdinalIgnoreCase));
                
                if (trainerExe != null)
                {
                    // Size Verification (if needed, currently just logging)
                    // var fileInfo = new FileInfo(trainerExe);
                    // Logger.Info($"Verified {trainerExe} size: {fileInfo.Length} bytes");

                    trainer.LocalExePath = trainerExe;
                    trainer.IsDownloaded = true;
                    
                    // Auto-launch if this was a direct user action (can be passed as parameter if needed)
                    // For now, we just return true and let UI decide
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to extract zip");
                // Clean up?
                return false;
            }
            
            return false;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to download trainer");
            return false;
        }
    }

    public Task<bool> LaunchTrainerAsync(Trainer trainer)
    {
        try
        {
            if (string.IsNullOrEmpty(trainer.LocalExePath) || !File.Exists(trainer.LocalExePath))
                return Task.FromResult(false);

            var startInfo = new ProcessStartInfo
            {
                FileName = trainer.LocalExePath,
                UseShellExecute = true, // Required for UAC prompt if needed
                WorkingDirectory = Path.GetDirectoryName(trainer.LocalExePath)
            };
            
            Process.Start(startInfo);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to launch trainer");
            return Task.FromResult(false);
        }
    }

    public Task<bool> DeleteTrainerAsync(Trainer trainer)
    {
        try
        {
            if (!string.IsNullOrEmpty(trainer.LocalExePath))
            {
                var folder = Path.GetDirectoryName(trainer.LocalExePath);
                if (Directory.Exists(folder))
                {
                    Directory.Delete(folder, true);
                }
            }
            trainer.IsDownloaded = false;
            trainer.LocalExePath = null;
            trainer.LocalZipPath = null;
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to delete trainer");
            return Task.FromResult(false);
        }
    }
}
