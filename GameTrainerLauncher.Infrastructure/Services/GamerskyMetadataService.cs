using System.Net.Http.Headers;
using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Interfaces;
using GameTrainerLauncher.Core.Models;
using GameTrainerLauncher.Core.Utilities;
using HtmlAgilityPack;
using NLog;

namespace GameTrainerLauncher.Infrastructure.Services;

public sealed class GamerskyMetadataService : IGameTitleMetadataService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly TimeSpan MinimumRequestInterval = TimeSpan.FromMilliseconds(220);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(40);
    private const int MaxSearchCandidates = 40;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _requestGate = new(1, 1);
    private DateTimeOffset _nextAllowedRequestUtc = DateTimeOffset.MinValue;

    public GamerskyMetadataService()
    {
        _httpClient = new HttpClient
        {
            Timeout = RequestTimeout
        };
        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.Add(ProductInfoHeaderValue.Parse("Mozilla/5.0"));
        _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("zh-CN,zh;q=0.9,en;q=0.8");
    }

    public async Task<IReadOnlyList<GameTitleSearchCandidate>> SearchCandidatesAsync(string query, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        try
        {
            return await ExecuteWithRetryAsync(async attemptCancellationToken =>
            {
                var requestUrl = $"https://so.gamersky.com/all/ku?s={Uri.EscapeDataString(query)}";
                var html = await GetStringWithRateLimitAsync(requestUrl, attemptCancellationToken);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var nodes = doc.DocumentNode.SelectNodes("//ul[contains(@class, 'ImgY')]//li/a[@href]")
                    ?? doc.DocumentNode.SelectNodes("//a[@href]");
                if (nodes == null)
                {
                    return Array.Empty<GameTitleSearchCandidate>();
                }

                var seenUrls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var candidates = new List<GameTitleSearchCandidate>();
                foreach (var node in nodes)
                {
                    var detailUrl = NormalizeDetailUrl(node.GetAttributeValue("href", string.Empty));
                    if (string.IsNullOrWhiteSpace(detailUrl) || !seenUrls.Add(detailUrl))
                    {
                        continue;
                    }

                    var title = ExtractCandidateTitle(node);
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        continue;
                    }

                    candidates.Add(new GameTitleSearchCandidate
                    {
                        SourceKey = detailUrl,
                        DetailUrl = detailUrl,
                        Title = title
                    });

                    if (candidates.Count >= MaxSearchCandidates)
                    {
                        break;
                    }
                }

                return (IReadOnlyList<GameTitleSearchCandidate>)candidates;
            }, $"search Gamersky metadata candidates for {query}", cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to search Gamersky candidates for {Query}.", query);
            return [];
        }
    }

    public async Task<GameTitleMetadata?> GetMetadataAsync(string sourceKeyOrUrl, CancellationToken cancellationToken = default)
    {
        var detailUrl = NormalizeDetailUrl(sourceKeyOrUrl);
        if (string.IsNullOrWhiteSpace(detailUrl))
        {
            return null;
        }

        try
        {
            return await ExecuteWithRetryAsync(async attemptCancellationToken =>
            {
                var html = await GetStringWithRateLimitAsync(detailUrl, attemptCancellationToken);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var chineseName = ExtractText(doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'tit_CH')]")?.InnerText)
                    ?? ExtractText(doc.DocumentNode.SelectSingleNode("//meta[@property='og:title']")?.GetAttributeValue("content", null));
                var englishName = ExtractText(doc.DocumentNode.SelectSingleNode("//div[contains(@class, 'tit_EN')]")?.InnerText);

                if (string.IsNullOrWhiteSpace(chineseName) && string.IsNullOrWhiteSpace(englishName))
                {
                    return null;
                }

                return new GameTitleMetadata
                {
                    Source = TrainerTitleIndexEntry.MetadataSourceGamersky,
                    SourceKey = detailUrl,
                    SourceUrl = detailUrl,
                    EnglishName = englishName,
                    ChineseName = chineseName
                };
            }, $"fetch Gamersky metadata for {detailUrl}", cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Failed to get Gamersky metadata for {DetailUrl}.", detailUrl);
            return null;
        }
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
            response.EnsureSuccessStatusCode();
            _nextAllowedRequestUtc = DateTimeOffset.UtcNow.Add(MinimumRequestInterval);
            return await response.Content.ReadAsStringAsync(cancellationToken);
        }
        finally
        {
            _requestGate.Release();
        }
    }

    private static string? NormalizeDetailUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (!uri.Host.Equals("ku.gamersky.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var clean = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        return string.IsNullOrWhiteSpace(clean)
            ? null
            : $"{clean}/";
    }

    private static string? ExtractCandidateTitle(HtmlNode node)
    {
        var rawTitle = node.SelectSingleNode(".//div[contains(@class, 'tit')]")?.InnerText;
        if (string.IsNullOrWhiteSpace(rawTitle))
        {
            rawTitle = node.SelectSingleNode(".//img")?.GetAttributeValue("title", null);
        }

        return ExtractText(rawTitle);
    }

    private static string? ExtractText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = HtmlEntity.DeEntitize(value).Trim();
        text = TitleSearchNormalizer.CollapseWhitespace(text);
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static async Task<T> ExecuteWithRetryAsync<T>(
        Func<CancellationToken, Task<T>> action,
        string operationName,
        CancellationToken cancellationToken,
        int maxRetries = 3)
    {
        for (var attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                return await action(cancellationToken);
            }
            catch (HttpRequestException ex) when (attempt < maxRetries - 1)
            {
                Logger.Warn(ex, "Attempt {Attempt} failed while trying to {Operation}.", attempt + 1, operationName);
                await Task.Delay(GetRetryDelay(attempt), cancellationToken);
            }
            catch (TaskCanceledException ex) when (attempt < maxRetries - 1)
            {
                Logger.Warn(ex, "Attempt {Attempt} timed out while trying to {Operation}.", attempt + 1, operationName);
                await Task.Delay(GetRetryDelay(attempt), cancellationToken);
            }
        }

        throw new InvalidOperationException($"Failed to {operationName} after {maxRetries} attempts.");
    }

    private static TimeSpan GetRetryDelay(int attempt)
    {
        var delays = new[]
        {
            TimeSpan.FromMilliseconds(400),
            TimeSpan.FromMilliseconds(800),
            TimeSpan.FromMilliseconds(1600)
        };
        return delays[Math.Min(attempt, delays.Length - 1)];
    }
}
