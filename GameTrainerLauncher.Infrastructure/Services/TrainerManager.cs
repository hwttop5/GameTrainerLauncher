using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Interfaces;
using GameTrainerLauncher.Core.Models;
using GameTrainerLauncher.Infrastructure;
using HtmlAgilityPack;
using NLog;

namespace GameTrainerLauncher.Infrastructure.Services;

public class TrainerManager : ITrainerManager
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private readonly HttpClient _httpClient;
    private const string FlingBaseUrl = "https://flingtrainer.com";

    private enum DownloadPayloadKind
    {
        Html,
        Zip,
        Exe,
        Binary
    }

    private sealed record DownloadPayload(
        byte[] Bytes,
        DownloadPayloadKind Kind,
        long BytesRead,
        long? BytesTotal);

    private sealed class DownloadProgressReporter
    {
        private readonly IProgress<TrainerDownloadProgress>? _progress;
        private double _lastPercent;

        public DownloadProgressReporter(IProgress<TrainerDownloadProgress>? progress)
        {
            _progress = progress;
        }

        public void ReportPreparing(double fraction, string statusText)
        {
            Report(TrainerDownloadStage.Preparing, MapStageProgress(fraction, 0, 10), 0, null, true, statusText);
        }

        public void ReportDownloading(long bytesReceived, long? bytesTotal)
        {
            if (bytesTotal is > 0)
            {
                var fraction = (double)bytesReceived / bytesTotal.Value;
                var percent = MapStageProgress(fraction, 10, 85);
                Report(
                    TrainerDownloadStage.Downloading,
                    percent,
                    bytesReceived,
                    bytesTotal,
                    false,
                    $"{FormatBytes(bytesReceived)} / {FormatBytes(bytesTotal.Value)}");
                return;
            }

            var estimatedFraction = EstimateUnknownDownloadFraction(bytesReceived);
            var percentUnknown = MapStageProgress(estimatedFraction, 10, 80);
            Report(
                TrainerDownloadStage.Downloading,
                percentUnknown,
                bytesReceived,
                null,
                true,
                $"{FormatBytes(bytesReceived)} · Size unknown");
        }

        public void ReportDownloadComplete(long bytesReceived, long? bytesTotal)
        {
            var statusText = bytesTotal is > 0
                ? $"{FormatBytes(bytesReceived)} / {FormatBytes(bytesTotal.Value)}"
                : $"{FormatBytes(bytesReceived)} · Size unknown";

            Report(
                TrainerDownloadStage.Downloading,
                85,
                bytesReceived,
                bytesTotal,
                bytesTotal is null,
                statusText);
        }

        public void ReportExtracting(int completedEntries, int totalEntries)
        {
            var fraction = totalEntries > 0 ? (double)completedEntries / totalEntries : 1d;
            var percent = MapStageProgress(fraction, 85, 98);
            var statusText = totalEntries > 0
                ? $"Extracting... {completedEntries}/{totalEntries}"
                : "Extracting...";

            Report(
                TrainerDownloadStage.Extracting,
                percent,
                0,
                null,
                true,
                statusText);
        }

        public void ReportFinalizing(double fraction, string statusText)
        {
            Report(TrainerDownloadStage.Finalizing, MapStageProgress(fraction, 98, 100), 0, null, true, statusText);
        }

        private void Report(
            TrainerDownloadStage stage,
            double percent,
            long bytesReceived,
            long? bytesTotal,
            bool isEstimated,
            string statusText)
        {
            if (_progress == null)
            {
                return;
            }

            var clamped = Math.Clamp(percent, 0, 100);
            if (clamped < _lastPercent)
            {
                clamped = _lastPercent;
            }

            _lastPercent = clamped;
            _progress.Report(new TrainerDownloadProgress(stage, clamped, bytesReceived, bytesTotal, isEstimated, statusText));
        }

        private static double MapStageProgress(double fraction, double stageStart, double stageEnd)
        {
            var boundedFraction = Math.Clamp(fraction, 0d, 1d);
            return stageStart + ((stageEnd - stageStart) * boundedFraction);
        }

        private static double EstimateUnknownDownloadFraction(long bytesReceived)
        {
            const double smoothingBytes = 8d * 1024d * 1024d;
            return 1d - Math.Exp(-bytesReceived / smoothingBytes);
        }

        private static string FormatBytes(long bytes)
        {
            string[] suffixes = ["B", "KB", "MB", "GB"];
            double size = bytes;
            var suffixIndex = 0;

            while (size >= 1024 && suffixIndex < suffixes.Length - 1)
            {
                size /= 1024;
                suffixIndex++;
            }

            var format = suffixIndex == 0 ? "0" : "0.0";
            return $"{size.ToString(format, CultureInfo.InvariantCulture)} {suffixes[suffixIndex]}";
        }
    }

    public TrainerManager()
    {
        _httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 5 });
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
    }

    private static string SanitizeFileName(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return "Trainer";
        }

        var invalid = Path.GetInvalidFileNameChars();
        var name = string.Join("_", title.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Trim();
        return string.IsNullOrEmpty(name) ? "Trainer" : name;
    }

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
                var href = node.GetAttributeValue("href", string.Empty)?.Trim().TrimEnd(';', ' ');
                if (!string.IsNullOrEmpty(href))
                {
                    if (!href.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        href = new Uri(new Uri(FlingBaseUrl), href).ToString();
                    }

                    return href;
                }
            }

            var metaRefresh = Regex.Match(html, @"<meta[^>]+http-equiv\s*=\s*[""']?refresh[""']?[^>]+content\s*=\s*[""']?\d+;\s*url=([^""'\s>]+)[""']?", RegexOptions.IgnoreCase);
            if (metaRefresh.Success && !string.IsNullOrWhiteSpace(metaRefresh.Groups[1].Value))
            {
                var url = metaRefresh.Groups[1].Value.Trim();
                if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    url = new Uri(new Uri(FlingBaseUrl), url).ToString();
                }

                return url;
            }

            var jsLocation = Regex.Match(html, @"window\.location\s*=\s*[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            if (jsLocation.Success && !string.IsNullOrWhiteSpace(jsLocation.Groups[1].Value))
            {
                var url = jsLocation.Groups[1].Value.Trim();
                if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                {
                    url = new Uri(new Uri(FlingBaseUrl), url).ToString();
                }

                return url;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task<bool> DownloadTrainerAsync(Trainer trainer, IProgress<TrainerDownloadProgress> progress, CancellationToken cancellationToken = default)
    {
        var reporter = new DownloadProgressReporter(progress);

        try
        {
            if (string.IsNullOrWhiteSpace(trainer.DownloadUrl))
            {
                Logger.Warn("DownloadUrl is empty for trainer: {Title}", trainer.Title);
                return false;
            }

            reporter.ReportPreparing(0, "Preparing download...");

            var folderName = SanitizeFileName(trainer.Title);
            var trainerFolder = Path.Combine(AppPaths.DataFolder, "Trainers", folderName);
            Directory.CreateDirectory(trainerFolder);
            var zipPath = Path.Combine(trainerFolder, "trainer.zip");

            reporter.ReportPreparing(0.2, "Connecting to download server...");

            var downloadUrl = trainer.DownloadUrl.Trim().TrimEnd(';', ' ');
            using var request = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
            request.Headers.TryAddWithoutValidation("Referer", trainer.PageUrl ?? FlingBaseUrl);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                Logger.Warn("Download first request failed for [{Title}]: Status={StatusCode}, Url={Url}", trainer.Title, response.StatusCode, downloadUrl);
                response.EnsureSuccessStatusCode();
            }

            reporter.ReportPreparing(0.4, "Inspecting download response...");

            var firstPayload = await ReadPayloadAsync(response, reporter, cancellationToken);
            if (firstPayload.Kind == DownloadPayloadKind.Exe)
            {
                return await SaveExecutableAsync(firstPayload.Bytes, trainer, trainerFolder, reporter, cancellationToken);
            }

            if (firstPayload.Kind == DownloadPayloadKind.Zip)
            {
                await File.WriteAllBytesAsync(zipPath, firstPayload.Bytes, cancellationToken);
                trainer.LocalZipPath = zipPath;
                return await ExtractTrainerAsync(zipPath, trainerFolder, trainer, reporter, cancellationToken);
            }

            reporter.ReportPreparing(0.8, "Resolving final download link...");

            var html = Encoding.UTF8.GetString(firstPayload.Bytes);
            var realZipUrl = TryGetZipUrlFromHtml(html, downloadUrl);
            if (string.IsNullOrEmpty(realZipUrl))
            {
                var preview = firstPayload.Bytes.Length > 300
                    ? Encoding.UTF8.GetString(firstPayload.Bytes, 0, 300) + "..."
                    : Encoding.UTF8.GetString(firstPayload.Bytes);
                Logger.Warn("Download returned HTML but no zip link found for [{Title}]. ContentLength={Len}, Preview={Preview}", trainer.Title, firstPayload.Bytes.Length, preview);
                return false;
            }

            reporter.ReportPreparing(1, "Download link resolved.");

            using var request2 = new HttpRequestMessage(HttpMethod.Get, realZipUrl);
            request2.Headers.TryAddWithoutValidation("Referer", trainer.PageUrl ?? FlingBaseUrl);
            using var response2 = await _httpClient.SendAsync(request2, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response2.IsSuccessStatusCode)
            {
                Logger.Warn("Download second request failed for [{Title}]: Status={StatusCode}, Url={Url}", trainer.Title, response2.StatusCode, realZipUrl);
                response2.EnsureSuccessStatusCode();
            }

            var secondPayload = await ReadPayloadAsync(response2, reporter, cancellationToken);
            if (secondPayload.Kind == DownloadPayloadKind.Exe)
            {
                return await SaveExecutableAsync(secondPayload.Bytes, trainer, trainerFolder, reporter, cancellationToken);
            }

            if (secondPayload.Kind != DownloadPayloadKind.Zip)
            {
                Logger.Warn("Second request also returned non-zip for {Title}", trainer.Title);
                return false;
            }

            await File.WriteAllBytesAsync(zipPath, secondPayload.Bytes, cancellationToken);
            trainer.LocalZipPath = zipPath;
            return await ExtractTrainerAsync(zipPath, trainerFolder, trainer, reporter, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to download trainer [{Title}]: {Message}", trainer?.Title ?? "(null)", ex.Message);
            return false;
        }
    }

    public Task<bool> LaunchTrainerAsync(Trainer trainer)
    {
        try
        {
            if (string.IsNullOrEmpty(trainer.LocalExePath) || !File.Exists(trainer.LocalExePath))
            {
                return Task.FromResult(false);
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = trainer.LocalExePath,
                UseShellExecute = true,
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
            var folder = !string.IsNullOrEmpty(trainer.LocalZipPath)
                ? Path.GetDirectoryName(trainer.LocalZipPath)
                : Path.GetDirectoryName(trainer.LocalExePath);

            if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder))
            {
                Directory.Delete(folder, true);
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

    private async Task<DownloadPayload> ReadPayloadAsync(
        HttpResponseMessage response,
        DownloadProgressReporter reporter,
        CancellationToken cancellationToken)
    {
        var contentType = response.Content.Headers.ContentType?.MediaType;
        var bytesTotal = response.Content.Headers.ContentLength > 0 ? response.Content.Headers.ContentLength : null;
        using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var memoryStream = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        long bytesRead = 0;
        DownloadPayloadKind? kind = null;

        while ((read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)) > 0)
        {
            await memoryStream.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            bytesRead += read;

            kind ??= DetectPayloadKind(buffer.AsSpan(0, read), contentType);
            if (kind != DownloadPayloadKind.Html)
            {
                reporter.ReportDownloading(bytesRead, bytesTotal);
            }
        }

        var bytes = memoryStream.ToArray();
        kind ??= DetectPayloadKind(bytes, contentType);

        if (kind != DownloadPayloadKind.Html)
        {
            reporter.ReportDownloadComplete(bytesRead, bytesTotal);
        }

        return new DownloadPayload(bytes, kind.Value, bytesRead, bytesTotal);
    }

    private async Task<bool> SaveExecutableAsync(
        byte[] bytes,
        Trainer trainer,
        string trainerFolder,
        DownloadProgressReporter reporter,
        CancellationToken cancellationToken)
    {
        reporter.ReportFinalizing(0.25, "Saving executable...");
        var exeFileName = SanitizeFileName(trainer.Title) + ".exe";
        var exePath = Path.Combine(trainerFolder, exeFileName);
        await File.WriteAllBytesAsync(exePath, bytes, cancellationToken);
        trainer.LocalExePath = exePath;
        trainer.IsDownloaded = true;
        reporter.ReportFinalizing(1, "Completed.");
        return true;
    }

    private Task<bool> ExtractTrainerAsync(
        string zipPath,
        string trainerFolder,
        Trainer trainer,
        DownloadProgressReporter reporter,
        CancellationToken cancellationToken)
    {
        try
        {
            trainer.LocalZipPath = zipPath;

            using var archive = ZipFile.OpenRead(zipPath);
            var fileEntries = archive.Entries
                .Where(entry => !string.IsNullOrEmpty(entry.Name))
                .ToList();

            reporter.ReportExtracting(0, fileEntries.Count);

            var trainerFolderPath = Path.GetFullPath(trainerFolder);
            for (var i = 0; i < fileEntries.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var entry = fileEntries[i];
                var destinationPath = Path.GetFullPath(Path.Combine(trainerFolder, entry.FullName));
                if (!destinationPath.StartsWith(trainerFolderPath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException($"Zip entry path escapes target folder: {entry.FullName}");
                }

                var destinationDirectory = Path.GetDirectoryName(destinationPath);
                if (!string.IsNullOrEmpty(destinationDirectory))
                {
                    Directory.CreateDirectory(destinationDirectory);
                }

                entry.ExtractToFile(destinationPath, true);
                reporter.ReportExtracting(i + 1, fileEntries.Count);
            }

            reporter.ReportFinalizing(0.25, "Scanning extracted files...");

            var exeFiles = Directory.GetFiles(trainerFolder, "*.exe", SearchOption.AllDirectories);
            var trainerExe = exeFiles.FirstOrDefault(file =>
                !Path.GetFileName(file).StartsWith("unins", StringComparison.OrdinalIgnoreCase) &&
                !Path.GetFileName(file).StartsWith("readme", StringComparison.OrdinalIgnoreCase));

            if (trainerExe == null)
            {
                return Task.FromResult(false);
            }

            trainer.LocalExePath = trainerExe;
            trainer.IsDownloaded = true;
            reporter.ReportFinalizing(1, "Completed.");
            return Task.FromResult(true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to extract zip");
            return Task.FromResult(false);
        }
    }

    private static DownloadPayloadKind DetectPayloadKind(ReadOnlySpan<byte> bytes, string? contentType)
    {
        if (bytes.Length >= 2)
        {
            if (bytes[0] == 0x50 && bytes[1] == 0x4B)
            {
                return DownloadPayloadKind.Zip;
            }

            if (bytes[0] == 0x4D && bytes[1] == 0x5A)
            {
                return DownloadPayloadKind.Exe;
            }
        }

        if (!string.IsNullOrWhiteSpace(contentType) &&
            (contentType.Contains("html", StringComparison.OrdinalIgnoreCase) ||
             contentType.Contains("xml", StringComparison.OrdinalIgnoreCase)))
        {
            return DownloadPayloadKind.Html;
        }

        var firstMeaningfulByte = GetFirstMeaningfulByte(bytes);
        if (firstMeaningfulByte == (byte)'<')
        {
            return DownloadPayloadKind.Html;
        }

        return DownloadPayloadKind.Binary;
    }

    private static byte? GetFirstMeaningfulByte(ReadOnlySpan<byte> bytes)
    {
        var index = 0;

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            index = 3;
        }

        while (index < bytes.Length)
        {
            var current = bytes[index];
            if (!char.IsWhiteSpace((char)current))
            {
                return current;
            }

            index++;
        }

        return null;
    }
}
