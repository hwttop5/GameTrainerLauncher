using System.Collections.Concurrent;
using System.Net.Http.Headers;
using GameTrainerLauncher.Core.Interfaces;
using NLog;

namespace GameTrainerLauncher.Infrastructure.Services;

public class GameCoverService : IGameCoverService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private const string FlingBaseUrl = "https://flingtrainer.com";
    private static readonly TimeSpan TrainerFailureCooldown = TimeSpan.FromMinutes(10);
    private static readonly ConcurrentDictionary<int, SemaphoreSlim> GameDownloadLocks = new();
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> TrainerDownloadLocks = new(StringComparer.Ordinal);
    private static readonly ConcurrentDictionary<string, DateTimeOffset> TrainerDownloadFailures = new(StringComparer.Ordinal);

    private readonly HttpClient _httpClient;

    public GameCoverService()
    {
        _httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 5 });
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GameTrainerLauncher", "1.0"));
        _httpClient.Timeout = TimeSpan.FromSeconds(25);
    }

    public bool HasCover(int gameId)
    {
        return GameCoverCache.TryGetCoverPath(gameId, out _);
    }

    public string GetCoverFilePath(int gameId, string? coverUrl = null)
    {
        AppPaths.EnsureCoversFolderExists();
        var ext = GuessExtensionFromUrlOrDefault(coverUrl);
        return Path.Combine(AppPaths.CoversFolder, $"game_{gameId}{ext}");
    }

    public async Task<bool> EnsureCoverAsync(int gameId, string? coverUrl, CancellationToken cancellationToken = default)
    {
        if (gameId <= 0)
        {
            return false;
        }

        var gate = GameDownloadLocks.GetOrAdd(gameId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (HasCover(gameId))
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(coverUrl))
            {
                return false;
            }

            var prefix = $"game_{gameId}";
            if (TryGetLocalFilePath(coverUrl, out var localPath))
            {
                var copiedPath = CopyLocalCover(prefix, localPath, coverUrl);
                if (!string.IsNullOrWhiteSpace(copiedPath))
                {
                    GameCoverCache.SetCoverPath(gameId, copiedPath);
                    return true;
                }

                return false;
            }

            var normalizedUrl = NormalizeFlingUrl(coverUrl);
            var savedPath = await DownloadImageAsync(prefix, normalizedUrl, coverUrl, cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(savedPath))
            {
                return false;
            }

            GameCoverCache.SetCoverPath(gameId, savedPath);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "EnsureCoverAsync failed for gameId={GameId}", gameId);
            return false;
        }
        finally
        {
            gate.Release();
        }
    }

    public bool TryGetTrainerCoverPath(string? pageUrl, string? imageUrl, out string path)
    {
        if (TryGetLocalFilePath(imageUrl, out path))
        {
            return true;
        }

        return GameCoverCache.TryGetTrainerCoverPath(pageUrl, imageUrl, out path);
    }

    public async Task<string?> EnsureTrainerCoverAsync(string? pageUrl, string? imageUrl, CancellationToken cancellationToken = default)
    {
        if (TryGetTrainerCoverPath(pageUrl, imageUrl, out var existingPath))
        {
            return existingPath;
        }

        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return null;
        }

        if (!GameCoverCache.TryCreateTrainerCacheKey(pageUrl, imageUrl, out var cacheKey))
        {
            return null;
        }

        if (IsInFailureCooldown(cacheKey))
        {
            return null;
        }

        var gate = TrainerDownloadLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (TryGetTrainerCoverPath(pageUrl, imageUrl, out existingPath))
            {
                TrainerDownloadFailures.TryRemove(cacheKey, out _);
                return existingPath;
            }

            if (IsInFailureCooldown(cacheKey))
            {
                return null;
            }

            if (TryGetLocalFilePath(imageUrl, out var localPath))
            {
                return localPath;
            }

            var normalizedUrl = NormalizeFlingUrl(imageUrl);
            if (!Uri.TryCreate(normalizedUrl, UriKind.Absolute, out var uri) ||
                uri.Scheme is not ("http" or "https"))
            {
                MarkTrainerFailure(cacheKey);
                return null;
            }

            var savedPath = await DownloadImageAsync($"trainer_{cacheKey}", normalizedUrl, imageUrl, cancellationToken)
                .ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(savedPath))
            {
                MarkTrainerFailure(cacheKey);
                return null;
            }

            GameCoverCache.SetTrainerCoverPath(cacheKey, savedPath);
            TrainerDownloadFailures.TryRemove(cacheKey, out _);
            return savedPath;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return null;
        }
        catch (Exception ex)
        {
            MarkTrainerFailure(cacheKey);
            Logger.Warn(ex, "EnsureTrainerCoverAsync failed for PageUrl={PageUrl}, ImageUrl={ImageUrl}", pageUrl, imageUrl);
            return null;
        }
        finally
        {
            gate.Release();
        }
    }

    public void DeleteCover(int gameId)
    {
        if (gameId > 0)
        {
            TryDeleteAllForPrefix($"game_{gameId}");
            GameCoverCache.Invalidate(gameId);
        }
    }

    private async Task<string?> DownloadImageAsync(
        string filePrefix,
        string normalizedUrl,
        string originalUrl,
        CancellationToken cancellationToken)
    {
        AppPaths.EnsureCoversFolderExists();

        using var req = new HttpRequestMessage(HttpMethod.Get, normalizedUrl);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));

        using var resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            Logger.Warn("Cover download failed: Status={StatusCode}, Url={Url}", resp.StatusCode, normalizedUrl);
            return null;
        }

        var ext = GuessExtension(resp.Content.Headers.ContentType?.MediaType, originalUrl);
        var destPath = Path.Combine(AppPaths.CoversFolder, $"{filePrefix}{ext}");
        var tempPath = Path.Combine(AppPaths.CoversFolder, $"{Guid.NewGuid():N}.tmp");

        try
        {
            await using (var contentStream = await resp.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
            await using (var fileStream = new FileStream(
                tempPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                useAsync: true))
            {
                await contentStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
            }

            if (new FileInfo(tempPath).Length == 0)
            {
                TryDeleteFile(tempPath);
                return null;
            }

            TryDeleteAllForPrefix(filePrefix);
            File.Move(tempPath, destPath, overwrite: true);
            return destPath;
        }
        catch
        {
            TryDeleteFile(tempPath);
            throw;
        }
    }

    private static string? CopyLocalCover(string filePrefix, string sourcePath, string? coverUrl)
    {
        AppPaths.EnsureCoversFolderExists();
        var ext = GuessExtensionFromUrlOrDefault(coverUrl ?? sourcePath);
        var destPath = Path.Combine(AppPaths.CoversFolder, $"{filePrefix}{ext}");
        var tempPath = Path.Combine(AppPaths.CoversFolder, $"{Guid.NewGuid():N}.tmp");

        try
        {
            File.Copy(sourcePath, tempPath, overwrite: false);
            if (new FileInfo(tempPath).Length == 0)
            {
                TryDeleteFile(tempPath);
                return null;
            }

            TryDeleteAllForPrefix(filePrefix);
            File.Move(tempPath, destPath, overwrite: true);
            return destPath;
        }
        catch
        {
            TryDeleteFile(tempPath);
            return null;
        }
    }

    private static void TryDeleteAllForPrefix(string filePrefix)
    {
        try
        {
            AppPaths.EnsureCoversFolderExists();
            var files = Directory.GetFiles(AppPaths.CoversFolder, $"{filePrefix}.*");
            foreach (var file in files)
            {
                TryDeleteFile(file);
            }
        }
        catch
        {
        }
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
        }
    }

    private static bool IsInFailureCooldown(string hash)
    {
        if (!TrainerDownloadFailures.TryGetValue(hash, out var lastFailure))
        {
            return false;
        }

        if (DateTimeOffset.UtcNow - lastFailure < TrainerFailureCooldown)
        {
            return true;
        }

        TrainerDownloadFailures.TryRemove(hash, out _);
        return false;
    }

    private static void MarkTrainerFailure(string hash)
    {
        TrainerDownloadFailures[hash] = DateTimeOffset.UtcNow;
    }

    private static bool TryGetLocalFilePath(string? value, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            var candidate = value.Trim();
            if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
            {
                if (!uri.IsFile)
                {
                    return false;
                }

                candidate = uri.LocalPath;
            }

            if (!Path.IsPathRooted(candidate) || !File.Exists(candidate))
            {
                return false;
            }

            path = candidate;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeFlingUrl(string url)
    {
        var u = url.Trim();
        if (u.StartsWith("/", StringComparison.Ordinal))
        {
            return FlingBaseUrl + u;
        }

        if (!u.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            return FlingBaseUrl + "/" + u.TrimStart('/');
        }

        return u;
    }

    private static string GuessExtensionFromUrlOrDefault(string? url)
    {
        var ext = GuessExtension(null, url);
        return string.IsNullOrWhiteSpace(ext) ? ".jpg" : ext;
    }

    private static string GuessExtension(string? contentType, string? url)
    {
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            if (contentType.Contains("png", StringComparison.OrdinalIgnoreCase))
            {
                return ".png";
            }

            if (contentType.Contains("jpeg", StringComparison.OrdinalIgnoreCase) ||
                contentType.Contains("jpg", StringComparison.OrdinalIgnoreCase))
            {
                return ".jpg";
            }

            if (contentType.Contains("gif", StringComparison.OrdinalIgnoreCase))
            {
                return ".gif";
            }

            if (contentType.Contains("bmp", StringComparison.OrdinalIgnoreCase))
            {
                return ".bmp";
            }

            if (contentType.Contains("webp", StringComparison.OrdinalIgnoreCase))
            {
                return ".webp";
            }
        }

        if (!string.IsNullOrWhiteSpace(url))
        {
            try
            {
                var u = url.Trim();
                if (u.StartsWith("/", StringComparison.Ordinal))
                {
                    u = FlingBaseUrl + u;
                }

                if (!u.StartsWith("http", StringComparison.OrdinalIgnoreCase) &&
                    !Path.IsPathRooted(u))
                {
                    u = FlingBaseUrl + "/" + u.TrimStart('/');
                }

                var path = Uri.TryCreate(u, UriKind.Absolute, out var uri)
                    ? uri.LocalPath
                    : u;
                var ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp")
                {
                    return ext == ".jpeg" ? ".jpg" : ext;
                }
            }
            catch
            {
            }
        }

        return ".jpg";
    }
}
