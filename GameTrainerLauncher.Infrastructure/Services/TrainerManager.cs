using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Interfaces;
using HtmlAgilityPack;
using NLog;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;

namespace GameTrainerLauncher.Infrastructure.Services;

public class TrainerManager : ITrainerManager
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly HttpClient _httpClient;
    private const string BaseFolder = "GameTrainerLauncher/Trainers";
    private const string FlingBaseUrl = "https://flingtrainer.com";

    public TrainerManager()
    {
        _httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 5 });
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

    private static string SanitizeFileName(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "Trainer";
        var invalid = Path.GetInvalidFileNameChars();
        var name = string.Join("_", title.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Trim();
        return string.IsNullOrEmpty(name) ? "Trainer" : name;
    }

    /// <summary>When download URL returns HTML, parse page for the actual zip/file link.</summary>
    private static string? TryGetZipUrlFromHtml(string html, string currentRequestUrl)
    {
        try
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var node = doc.DocumentNode.SelectSingleNode("//a[contains(@href,'.zip')]")
                ?? doc.DocumentNode.SelectSingleNode("//a[contains(@href,'/downloads/') and (contains(@href,'/download') or contains(@href,'/file'))]")
                ?? doc.DocumentNode.SelectSingleNode("//a[contains(@href,'/downloads/')]")
                ?? doc.DocumentNode.SelectSingleNode("//a[contains(@href,'download') and not(contains(@href,'flingtrainer.com/trainer/'))]");
            if (node != null)
            {
                var href = node.GetAttributeValue("href", "")?.Trim().TrimEnd(';', ' ');
                if (!string.IsNullOrEmpty(href))
                {
                    if (!href.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                        href = new Uri(new Uri(FlingBaseUrl), href).ToString();
                    return href;
                }
            }
            var metaRefresh = Regex.Match(html, @"<meta[^>]+http-equiv\s*=\s*[""']?refresh[""']?[^>]+content\s*=\s*[""']?\d+;\s*url=([^""'\s>]+)[""']?", RegexOptions.IgnoreCase);
            if (metaRefresh.Success && !string.IsNullOrWhiteSpace(metaRefresh.Groups[1].Value))
            {
                var u = metaRefresh.Groups[1].Value.Trim();
                if (!u.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    u = new Uri(new Uri(FlingBaseUrl), u).ToString();
                return u;
            }
            var jsLocation = Regex.Match(html, @"window\.location\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            if (jsLocation.Success && !string.IsNullOrWhiteSpace(jsLocation.Groups[1].Value))
            {
                var u = jsLocation.Groups[1].Value.Trim();
                if (!u.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    u = new Uri(new Uri(FlingBaseUrl), u).ToString();
                return u;
            }
            // Do NOT fallback to currentRequestUrl + "/download" - that often returns 404
            return null;
        }
        catch { return null; }
    }

    /// <summary>
    /// 点击下载后的核心逻辑：
    /// 1) 用 Trainer.DownloadUrl 发 GET 请求；Referer 为 PageUrl。
    /// 2) 若响应体为 ZIP (PK)：保存为 trainer.zip，解压到以标题命名的文件夹，在文件夹中查找 .exe，赋值 LocalExePath。
    /// 3) 若响应体为 EXE (MZ)：直接保存为 "{Trainer.Title}.exe"（如 Slay the Spire 2 Trainer.exe），赋值 LocalExePath。
    /// 4) 若响应体为 HTML：解析页面中的 .zip 或 /downloads/ 链接，用解析出的 URL 再发一次 GET；若第二次为 ZIP 则解压找 exe，若为 EXE 则直接保存。
    /// 5) 若解析不到链接或第二次仍非 ZIP/EXE：返回 false（不再盲目拼 /download 避免 404）。
    /// </summary>
    public async Task<bool> DownloadTrainerAsync(Trainer trainer, IProgress<double> progress, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrEmpty(trainer.DownloadUrl))
            {
                Logger.Warn("DownloadUrl is empty for trainer: {Title}", trainer.Title);
                return false;
            }

            // 下载目录：<程序运行目录>\Data\Trainers\<Trainer标题_下划线>\，例如 ...\bin\Debug\net8.0-windows\Data\Trainers\Slay_the_Spire_2_Trainer\
            var localAppData = AppDomain.CurrentDomain.BaseDirectory;
            var trainerFolder = Path.Combine(localAppData, "Data", "Trainers", trainer.Title.Replace(" ", "_"));
            Directory.CreateDirectory(trainerFolder);

            var zipPath = Path.Combine(trainerFolder, "trainer.zip");

            var downloadUrl = trainer.DownloadUrl!.Trim().TrimEnd(';', ' ');
            using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            request.Headers.TryAddWithoutValidation("Referer", trainer.PageUrl ?? FlingBaseUrl);
            using (var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();
                var contentType = response.Content.Headers.ContentType?.MediaType ?? "";
                var totalBytes = response.Content.Headers.ContentLength ?? -1L;
                var canReportProgress = totalBytes != -1 && totalBytes > 0;
                using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var memStream = new MemoryStream();
                var copyBuf = new byte[8192];
                int read;
                long totalRead = 0;
                while ((read = await contentStream.ReadAsync(copyBuf, 0, copyBuf.Length, cancellationToken)) > 0)
                {
                    await memStream.WriteAsync(copyBuf, 0, read, cancellationToken);
                    totalRead += read;
                    if (canReportProgress) progress.Report((double)totalRead / totalBytes! * 100);
                }
                var bytes = memStream.ToArray();
                var isZip = bytes.Length >= 2 && bytes[0] == 0x50 && bytes[1] == 0x4B; // PK
                var isExe = bytes.Length >= 2 && bytes[0] == 0x4D && bytes[1] == 0x5A; // MZ
                if (isExe)
                {
                    // Server returned a single EXE (no ZIP). Save as e.g. "Slay the Spire 2 Trainer.exe"
                    var exeFileName = SanitizeFileName(trainer.Title) + ".exe";
                    var exePath = Path.Combine(trainerFolder, exeFileName);
                    await File.WriteAllBytesAsync(exePath, bytes, cancellationToken);
                    trainer.LocalExePath = exePath;
                    trainer.IsDownloaded = true;
                    return true;
                }
                if (!isZip)
                {
                    var html = System.Text.Encoding.UTF8.GetString(bytes);
                    var realZipUrl = TryGetZipUrlFromHtml(html, downloadUrl);
                    if (string.IsNullOrEmpty(realZipUrl))
                    {
                        Logger.Warn("Download returned HTML but no zip link found for {Title}", trainer.Title);
                        return false;
                    }
                    using var req2 = new HttpRequestMessage(HttpMethod.Get, realZipUrl);
                    req2.Headers.TryAddWithoutValidation("Referer", trainer.PageUrl ?? FlingBaseUrl);
                    using var resp2 = await _httpClient.SendAsync(req2, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                    resp2.EnsureSuccessStatusCode();
                    var totalBytes2 = resp2.Content.Headers.ContentLength ?? -1L;
                    var canReportProgress2 = totalBytes2 != -1 && totalBytes2 > 0;
                    using var stream2 = await resp2.Content.ReadAsStreamAsync(cancellationToken);
                    using var mem2 = new MemoryStream();
                    totalRead = 0;
                    while ((read = await stream2.ReadAsync(copyBuf, 0, copyBuf.Length, cancellationToken)) > 0)
                    {
                        await mem2.WriteAsync(copyBuf, 0, read, cancellationToken);
                        totalRead += read;
                        if (canReportProgress2) progress.Report((double)totalRead / totalBytes2 * 100);
                    }
                    var bytes2 = mem2.ToArray();
                    if (bytes2.Length >= 2 && bytes2[0] == 0x50 && bytes2[1] == 0x4B)
                        await File.WriteAllBytesAsync(zipPath, bytes2, cancellationToken);
                    else if (bytes2.Length >= 2 && bytes2[0] == 0x4D && bytes2[1] == 0x5A)
                    {
                        var exeFileName = SanitizeFileName(trainer.Title) + ".exe";
                        var exePath = Path.Combine(trainerFolder, exeFileName);
                        await File.WriteAllBytesAsync(exePath, bytes2, cancellationToken);
                        trainer.LocalExePath = exePath;
                        trainer.IsDownloaded = true;
                        return true;
                    }
                    else
                    {
                        Logger.Warn("Second request also returned non-zip for {Title}", trainer.Title);
                        return false;
                    }
                }
                else
                {
                    await File.WriteAllBytesAsync(zipPath, bytes, cancellationToken);
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
                    trainer.LocalExePath = trainerExe;
                    trainer.IsDownloaded = true;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Error(ex, "Failed to extract zip");
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
