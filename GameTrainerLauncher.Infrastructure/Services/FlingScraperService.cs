using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Interfaces;
using HtmlAgilityPack;
using NLog;
using System.Text.RegularExpressions;

namespace GameTrainerLauncher.Infrastructure.Services;

public class FlingScraperService : IScraperService
{
    private readonly HttpClient _httpClient;
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private const string BaseUrl = "https://flingtrainer.com";

    public FlingScraperService()
    {
        _httpClient = new HttpClient();
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.Timeout = TimeSpan.FromSeconds(20);
    }

    public async Task<List<Trainer>> GetPopularTrainersAsync(int page = 1)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var popularUrl = page == 1 ? $"{BaseUrl}/category/trainer/" : $"{BaseUrl}/category/trainer/page/{page}/";
            var html = await _httpClient.GetStringAsync(popularUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var trainers = new List<Trainer>();
            var nodes = doc.DocumentNode.SelectNodes("//article[contains(@class, 'post')]");

            if (nodes != null)
            {
                foreach (var node in nodes.Take(20))
                {
                    var titleNode = node.SelectSingleNode(".//h2/a");
                    if (titleNode == null) continue;

                    var link = titleNode.GetAttributeValue("href", "");
                    var title = System.Net.WebUtility.HtmlDecode(titleNode.InnerText.Trim());
                    
                    // Try to get image
                    var imgNode = node.SelectSingleNode(".//img");
                    var coverUrl = imgNode?.GetAttributeValue("src", "") ?? "";

                    // Parse "Last Updated: 2026.03.07" from article text
                    var blockText = node.InnerText ?? "";
                    var lastUpdated = ParseLastUpdatedFromText(blockText);

                    trainers.Add(new Trainer
                    {
                        Title = title,
                        PageUrl = link,
                        IsDownloaded = false,
                        ImageUrl = coverUrl,
                        LastUpdated = lastUpdated
                    });
                }
            }
            return trainers;
        }, "get popular trainers");
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action, string operationName, int maxRetries = 3)
    {
        for (int i = 0; i < maxRetries; i++)
        {
            try
            {
                return await action();
            }
            catch (HttpRequestException ex)
            {
                if (i == maxRetries - 1)
                {
                    Logger.Error(ex, $"Failed to {operationName} after {maxRetries} attempts.");
                    throw;
                }
                Logger.Warn(ex, $"Attempt {i + 1} failed for {operationName}. Retrying...");
                await Task.Delay(1000 * (i + 1)); // Exponential backoff
            }
            catch (Exception ex)
            {
                Logger.Error(ex, $"Unexpected error during {operationName}");
                throw;
            }
        }
        return default!;
    }

    /// <summary>Parse "Last Updated: 2026.03.07" from page text.</summary>
    private static DateTime? ParseLastUpdatedFromText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var match = Regex.Match(text, @"Last Updated:\s*(\d{4}\.\d{2}\.\d{2})", RegexOptions.IgnoreCase);
        if (!match.Success) return null;
        if (DateTime.TryParseExact(match.Groups[1].Value, "yyyy.MM.dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dt))
            return dt;
        return null;
    }

    public async Task<List<Trainer>> SearchAsync(string keyword)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var searchUrl = $"{BaseUrl}/?s={Uri.EscapeDataString(keyword)}";
            var html = await _httpClient.GetStringAsync(searchUrl);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var trainers = new List<Trainer>();
            var nodes = doc.DocumentNode.SelectNodes("//article[contains(@class, 'post')]");

            if (nodes != null)
            {
                foreach (var node in nodes.Take(10))
                {
                    var titleNode = node.SelectSingleNode(".//h2/a");
                    if (titleNode == null) continue;

                    var link = titleNode.GetAttributeValue("href", "");
                    var title = System.Net.WebUtility.HtmlDecode(titleNode.InnerText.Trim());

                    // Try to get image
                    var imgNode = node.SelectSingleNode(".//img");
                    var coverUrl = imgNode?.GetAttributeValue("src", "") ?? "";

                    // Parse "Last Updated: 2026.03.07" from article text
                    var blockText = node.InnerText ?? "";
                    var lastUpdated = ParseLastUpdatedFromText(blockText);

                    trainers.Add(new Trainer
                    {
                        Title = title,
                        PageUrl = link,
                        IsDownloaded = false,
                        ImageUrl = coverUrl,
                        LastUpdated = lastUpdated
                    });
                }
            }
            return trainers;
        }, $"search for {keyword}");
    }

    public async Task<Trainer> GetTrainerDetailsAsync(string url)
    {
        return await ExecuteWithRetryAsync(async () =>
        {
            var html = await _httpClient.GetStringAsync(url);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var titleNode = doc.DocumentNode.SelectSingleNode("//h1[contains(@class, 'entry-title')]");
            var title = System.Net.WebUtility.HtmlDecode(titleNode?.InnerText.Trim() ?? "Unknown Trainer");
            
            string? downloadUrl = null;
            
            // Try to find the specific "Standalone Versions" section if mentioned
            // But usually Fling has attachments.
            // Let's look for any link that looks like an attachment download
            var attachmentLinks = doc.DocumentNode.SelectNodes("//a[contains(@href, 'attachment_id')]");
            
            if (attachmentLinks != null && attachmentLinks.Count > 0)
            {
                 // Take the first one as requested ("first download link")
                 downloadUrl = attachmentLinks[0].GetAttributeValue("href", "");
            }
            else
            {
                // Fallback: look for any zip link
                var zipLink = doc.DocumentNode.SelectSingleNode("//a[contains(@href, '.zip')]");
                if (zipLink != null)
                {
                    downloadUrl = zipLink.GetAttributeValue("href", "");
                }
            }

            // Parse "Last Updated: 2026.03.07" from page content
            var bodyText = doc.DocumentNode.InnerText ?? "";
            var lastUpdated = ParseLastUpdatedFromText(bodyText) ?? DateTime.Now;

            return new Trainer
            {
                Title = title,
                PageUrl = url,
                DownloadUrl = downloadUrl,
                IsDownloaded = false,
                LastUpdated = lastUpdated
            };
        }, $"get details for {url}");
    }
}
