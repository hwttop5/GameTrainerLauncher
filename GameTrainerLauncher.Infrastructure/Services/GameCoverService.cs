using GameTrainerLauncher.Core.Interfaces;
using NLog;
using System.Net.Http.Headers;

namespace GameTrainerLauncher.Infrastructure.Services;

public class GameCoverService : IGameCoverService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private const string FlingBaseUrl = "https://flingtrainer.com";

    private readonly HttpClient _httpClient;

    public GameCoverService()
    {
        _httpClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = true, MaxAutomaticRedirections = 5 });
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GameTrainerLauncher", "1.0"));
        _httpClient.Timeout = TimeSpan.FromSeconds(25);
    }

    public bool HasCover(int gameId)
    {
        try
        {
            if (gameId <= 0) return false;
            GameTrainerLauncher.Infrastructure.AppPaths.EnsureCoversFolderExists();
            var pattern = Path.Combine(GameTrainerLauncher.Infrastructure.AppPaths.CoversFolder, $"game_{gameId}.*");
            return Directory.GetFiles(GameTrainerLauncher.Infrastructure.AppPaths.CoversFolder, $"game_{gameId}.*").Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public string GetCoverFilePath(int gameId, string? coverUrl = null)
    {
        GameTrainerLauncher.Infrastructure.AppPaths.EnsureCoversFolderExists();
        var ext = GuessExtensionFromUrlOrDefault(coverUrl);
        return Path.Combine(GameTrainerLauncher.Infrastructure.AppPaths.CoversFolder, $"game_{gameId}{ext}");
    }

    public async Task<bool> EnsureCoverAsync(int gameId, string? coverUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            if (gameId <= 0) return false;
            if (HasCover(gameId)) return true;
            if (string.IsNullOrWhiteSpace(coverUrl)) return false;

            GameTrainerLauncher.Infrastructure.AppPaths.EnsureCoversFolderExists();
            var normalizedUrl = NormalizeFlingUrl(coverUrl);

            using var req = new HttpRequestMessage(HttpMethod.Get, normalizedUrl);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("image/*"));

            using var resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!resp.IsSuccessStatusCode)
            {
                Logger.Warn("Cover download failed: Status={StatusCode}, Url={Url}", resp.StatusCode, normalizedUrl);
                return false;
            }

            var bytes = await resp.Content.ReadAsByteArrayAsync(cancellationToken);
            if (bytes.Length == 0) return false;

            var ext = GuessExtension(resp.Content.Headers.ContentType?.MediaType, coverUrl);
            var destPath = Path.Combine(GameTrainerLauncher.Infrastructure.AppPaths.CoversFolder, $"game_{gameId}{ext}");

            // 清理旧扩展名残留（比如先存了 .jpg 后又改为 .png）
            TryDeleteAllForGame(gameId);

            await File.WriteAllBytesAsync(destPath, bytes, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "EnsureCoverAsync failed for gameId={GameId}", gameId);
            return false;
        }
    }

    public void DeleteCover(int gameId)
    {
        if (gameId <= 0) return;
        TryDeleteAllForGame(gameId);
    }

    private static void TryDeleteAllForGame(int gameId)
    {
        try
        {
            GameTrainerLauncher.Infrastructure.AppPaths.EnsureCoversFolderExists();
            var files = Directory.GetFiles(GameTrainerLauncher.Infrastructure.AppPaths.CoversFolder, $"game_{gameId}.*");
            foreach (var f in files)
            {
                try { File.Delete(f); } catch { }
            }
        }
        catch { }
    }

    private static string NormalizeFlingUrl(string url)
    {
        var u = url.Trim();
        if (u.StartsWith("/", StringComparison.Ordinal))
            return FlingBaseUrl + u;
        if (!u.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            return FlingBaseUrl + "/" + u.TrimStart('/');
        return u;
    }

    private static string GuessExtensionFromUrlOrDefault(string? url)
    {
        var ext = GuessExtension(null, url);
        return string.IsNullOrWhiteSpace(ext) ? ".jpg" : ext;
    }

    private static string GuessExtension(string? contentType, string? url)
    {
        // 优先 Content-Type
        if (!string.IsNullOrWhiteSpace(contentType))
        {
            if (contentType.Contains("png", StringComparison.OrdinalIgnoreCase)) return ".png";
            if (contentType.Contains("jpeg", StringComparison.OrdinalIgnoreCase) || contentType.Contains("jpg", StringComparison.OrdinalIgnoreCase)) return ".jpg";
            if (contentType.Contains("gif", StringComparison.OrdinalIgnoreCase)) return ".gif";
            if (contentType.Contains("bmp", StringComparison.OrdinalIgnoreCase)) return ".bmp";
            if (contentType.Contains("webp", StringComparison.OrdinalIgnoreCase)) return ".webp";
        }

        // 再从 URL 推断
        if (!string.IsNullOrWhiteSpace(url))
        {
            try
            {
                var u = url.Trim();
                if (u.StartsWith("/", StringComparison.Ordinal))
                    u = FlingBaseUrl + u;
                if (!u.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    u = FlingBaseUrl + "/" + u.TrimStart('/');
                var uri = new Uri(u, UriKind.Absolute);
                var p = uri.AbsolutePath;
                var idx = p.LastIndexOf('.');
                if (idx >= 0 && idx < p.Length - 1)
                {
                    var ext = p[idx..].ToLowerInvariant();
                    if (ext is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp")
                        return ext == ".jpeg" ? ".jpg" : ext;
                }
            }
            catch { }
        }

        return ".jpg";
    }
}

