using System.Text.Json;
using System.Text.RegularExpressions;
using GameTrainerLauncher.Core.Interfaces;
using GameTrainerLauncher.Core.Models;
using HtmlAgilityPack;
using NLog;

namespace GameTrainerLauncher.Infrastructure.Services;

public class SteamStoreMetadataService : ISteamStoreMetadataService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly TimeSpan MinimumRequestInterval = TimeSpan.FromMilliseconds(1200);
    private const int MaxSearchCandidates = 50;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _requestGate = new(1, 1);
    private DateTimeOffset _nextAllowedRequestUtc = DateTimeOffset.MinValue;

    public SteamStoreMetadataService()
    {
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20)
        };
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
    }

    public async Task<IReadOnlyList<SteamStoreSearchCandidate>> SearchAppsAsync(string keyword, CancellationToken cancellationToken = default)
    {
        return await SearchAppsAsync(keyword, "english", cancellationToken);
    }

    public async Task<IReadOnlyList<SteamStoreSearchCandidate>> SearchAppsAsync(string keyword, string language, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return [];
        }

        return await ExecuteWithRetryAsync(async attemptCancellationToken =>
        {
            var requestUrl = $"https://store.steampowered.com/search/?term={Uri.EscapeDataString(keyword)}&l={Uri.EscapeDataString(language)}&ndl=1";
            var html = await GetStringWithRateLimitAsync(requestUrl, attemptCancellationToken);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var results = new List<SteamStoreSearchCandidate>();
            var rows = doc.DocumentNode.SelectNodes("//a[contains(@class, 'search_result_row')]");
            if (rows == null)
            {
                return results;
            }

            foreach (var row in rows)
            {
                var title = HtmlEntity.DeEntitize(
                    row.SelectSingleNode(".//span[contains(@class, 'title')]")?.InnerText ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(title))
                {
                    continue;
                }

                var appId = row.GetAttributeValue("data-ds-appid", string.Empty);
                if (string.IsNullOrWhiteSpace(appId))
                {
                    var href = row.GetAttributeValue("href", string.Empty);
                    var match = Regex.Match(href, @"/app/(?<id>\d+)/", RegexOptions.CultureInvariant);
                    if (match.Success)
                    {
                        appId = match.Groups["id"].Value;
                    }
                }

                var digits = Regex.Match(appId, @"\d+", RegexOptions.CultureInvariant).Value;
                if (string.IsNullOrWhiteSpace(digits))
                {
                    continue;
                }

                if (results.Any(result => result.AppId == digits))
                {
                    continue;
                }

                results.Add(new SteamStoreSearchCandidate
                {
                    AppId = digits,
                    Title = title
                });

                if (results.Count >= MaxSearchCandidates)
                {
                    break;
                }
            }

            return results;
        }, $"search Steam apps for {keyword}", cancellationToken);
    }

    public async Task<SteamAppMetadata?> GetAppMetadataAsync(string appId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(appId))
        {
            return null;
        }

        try
        {
            var englishName = await GetLocalizedNameAsync(appId, "english", cancellationToken);
            var chineseName = await GetLocalizedNameAsync(appId, "schinese", cancellationToken);
            if (string.IsNullOrWhiteSpace(englishName) && string.IsNullOrWhiteSpace(chineseName))
            {
                return null;
            }

            return new SteamAppMetadata
            {
                AppId = appId,
                EnglishName = englishName,
                ChineseName = chineseName
            };
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to get Steam app metadata for {AppId}.", appId);
            return null;
        }
    }

    private async Task<string?> GetLocalizedNameAsync(string appId, string language, CancellationToken cancellationToken)
    {
        var requestUrl = $"https://store.steampowered.com/api/appdetails?appids={Uri.EscapeDataString(appId)}&l={Uri.EscapeDataString(language)}";
        var json = await GetStringWithRateLimitAsync(requestUrl, cancellationToken);
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty(appId, out var appNode) ||
            !appNode.TryGetProperty("success", out var successNode) ||
            !successNode.GetBoolean() ||
            !appNode.TryGetProperty("data", out var dataNode) ||
            !dataNode.TryGetProperty("name", out var nameNode))
        {
            return null;
        }

        var value = nameNode.GetString();
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private async Task<string> GetStringWithRateLimitAsync(string requestUrl, CancellationToken cancellationToken)
    {
        await _requestGate.WaitAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            if (_nextAllowedRequestUtc > now)
            {
                await Task.Delay(_nextAllowedRequestUtc - now, cancellationToken);
            }

            using var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            if ((int)response.StatusCode == 429)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(15);
                _nextAllowedRequestUtc = DateTimeOffset.UtcNow.Add(retryAfter);
                throw new SteamRateLimitException(retryAfter);
            }

            response.EnsureSuccessStatusCode();
            _nextAllowedRequestUtc = DateTimeOffset.UtcNow.Add(MinimumRequestInterval);
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        finally
        {
            _requestGate.Release();
        }
    }

    private async Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> action,
        string operationName,
        CancellationToken cancellationToken,
        int maxRetries = 4)
    {
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                return await action(cancellationToken);
            }
            catch (SteamRateLimitException ex) when (attempt < maxRetries - 1)
            {
                Logger.Warn("Steam rate limited {Operation}. Waiting {DelaySeconds} seconds before retry {Attempt}.",
                    operationName,
                    Math.Ceiling(ex.RetryAfter.TotalSeconds),
                    attempt + 2);
                await Task.Delay(ex.RetryAfter, cancellationToken);
            }
            catch (HttpRequestException ex) when (attempt < maxRetries - 1)
            {
                Logger.Warn(ex, "Attempt {Attempt} failed while trying to {Operation}.", attempt + 1, operationName);
                await Task.Delay(TimeSpan.FromSeconds((attempt + 1) * 2), cancellationToken);
            }
        }

        throw new InvalidOperationException($"Failed to {operationName} after {maxRetries} attempts.");
    }

    private sealed class SteamRateLimitException : Exception
    {
        public SteamRateLimitException(TimeSpan retryAfter)
            : base($"Steam rate limited the request. Retry after {retryAfter}.")
        {
            RetryAfter = retryAfter;
        }

        public TimeSpan RetryAfter { get; }
    }
}
