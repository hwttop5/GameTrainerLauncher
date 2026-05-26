using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace GameTrainerLauncher.Infrastructure;

public static class GameCoverCache
{
    private const string MissingCoverPath = "";
    private static readonly ConcurrentDictionary<int, string> CoverPathsByGameId = new();
    private static readonly ConcurrentDictionary<string, string> CoverPathsByTrainerKey = new(StringComparer.Ordinal);

    public static bool TryGetCoverPath(int gameId, out string path)
    {
        path = string.Empty;

        if (gameId <= 0)
        {
            return false;
        }

        var cachedPath = CoverPathsByGameId.GetOrAdd(gameId, ResolveCoverPath);
        if (!string.IsNullOrWhiteSpace(cachedPath) && File.Exists(cachedPath))
        {
            path = cachedPath;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(cachedPath))
        {
            CoverPathsByGameId.TryRemove(gameId, out _);
        }

        return false;
    }

    public static void SetCoverPath(int gameId, string? path)
    {
        if (gameId <= 0 || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        CoverPathsByGameId[gameId] = path;
    }

    public static void Invalidate(int gameId)
    {
        if (gameId <= 0)
        {
            return;
        }

        CoverPathsByGameId.TryRemove(gameId, out _);
    }

    public static bool TryCreateTrainerCacheKey(string? pageUrl, string? imageUrl, out string cacheKey)
    {
        cacheKey = string.Empty;

        var source = NormalizeCacheSource(pageUrl) ?? NormalizeCacheSource(imageUrl);
        if (string.IsNullOrWhiteSpace(source))
        {
            return false;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        cacheKey = Convert.ToHexString(hash).ToLowerInvariant();
        return true;
    }

    public static bool TryGetTrainerCoverPath(string? pageUrl, string? imageUrl, out string path)
    {
        path = string.Empty;

        if (!TryCreateTrainerCacheKey(pageUrl, imageUrl, out var cacheKey))
        {
            return false;
        }

        return TryGetTrainerCoverPath(cacheKey, out path);
    }

    public static bool TryGetTrainerCoverPath(string cacheKey, out string path)
    {
        path = string.Empty;

        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return false;
        }

        var cachedPath = CoverPathsByTrainerKey.GetOrAdd(cacheKey, ResolveTrainerCoverPath);
        if (!string.IsNullOrWhiteSpace(cachedPath) && File.Exists(cachedPath))
        {
            path = cachedPath;
            return true;
        }

        if (!string.IsNullOrWhiteSpace(cachedPath))
        {
            CoverPathsByTrainerKey.TryRemove(cacheKey, out _);
        }

        return false;
    }

    public static void SetTrainerCoverPath(string cacheKey, string? path)
    {
        if (string.IsNullOrWhiteSpace(cacheKey) || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        CoverPathsByTrainerKey[cacheKey] = path;
    }

    public static void InvalidateTrainer(string cacheKey)
    {
        if (string.IsNullOrWhiteSpace(cacheKey))
        {
            return;
        }

        CoverPathsByTrainerKey.TryRemove(cacheKey, out _);
    }

    public static string GetTrainerCoverFilePattern(string cacheKey)
    {
        return $"trainer_{cacheKey}.*";
    }

    public static string GetTrainerCoverFileName(string cacheKey, string extension)
    {
        return $"trainer_{cacheKey}{extension}";
    }

    private static string ResolveCoverPath(int gameId)
    {
        try
        {
            AppPaths.EnsureCoversFolderExists();
            var file = Directory.EnumerateFiles(AppPaths.CoversFolder, $"game_{gameId}.*", SearchOption.TopDirectoryOnly)
                .FirstOrDefault();
            return file ?? MissingCoverPath;
        }
        catch
        {
            return MissingCoverPath;
        }
    }

    private static string ResolveTrainerCoverPath(string cacheKey)
    {
        try
        {
            AppPaths.EnsureCoversFolderExists();
            var file = Directory.EnumerateFiles(
                    AppPaths.CoversFolder,
                    GetTrainerCoverFilePattern(cacheKey),
                    SearchOption.TopDirectoryOnly)
                .FirstOrDefault();
            return file ?? MissingCoverPath;
        }
        catch
        {
            return MissingCoverPath;
        }
    }

    private static string? NormalizeCacheSource(string? source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            return null;
        }

        var trimmed = source.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri))
        {
            if (uri.IsFile)
            {
                return uri.LocalPath.Trim().ToLowerInvariant();
            }

            var builder = new UriBuilder(uri)
            {
                Scheme = uri.Scheme.ToLowerInvariant(),
                Host = uri.Host.ToLowerInvariant(),
                Fragment = string.Empty
            };

            var normalized = builder.Uri.ToString().TrimEnd('/');
            return normalized.ToLowerInvariant();
        }

        return trimmed.TrimEnd('/').ToLowerInvariant();
    }
}
