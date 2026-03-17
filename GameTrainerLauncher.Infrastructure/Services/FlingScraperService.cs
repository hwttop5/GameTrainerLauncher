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

            var title = System.Net.WebUtility.HtmlDecode(
                doc.DocumentNode.SelectSingleNode("//h1[contains(@class, 'entry-title')]")?.InnerText.Trim()
                ?? doc.DocumentNode.SelectSingleNode("//meta[@property='og:title']")?.GetAttributeValue("content", "")?.Trim()
                ?? doc.DocumentNode.SelectSingleNode("//h1")?.InnerText.Trim()
                ?? doc.DocumentNode.SelectSingleNode("//title")?.InnerText?.Replace(" - Fling Trainer", "").Trim()
                ?? "Unknown Trainer");

            // Cover image: og:image or first post image
            var imageUrl = doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']")?.GetAttributeValue("content", "")
                ?? doc.DocumentNode.SelectSingleNode("//article//img")?.GetAttributeValue("src", "")
                ?? "";

            string? downloadUrl = null;
            DateTime? lastUpdated = null;

            // "Standalone Versions" / Download table: first row's link is the latest trainer
            var tables = doc.DocumentNode.SelectNodes("//table");
            if (tables != null)
            {
                foreach (var table in tables)
                {
                    var rows = table.SelectNodes(".//tr");
                    if (rows == null || rows.Count < 2) continue;
                    var headerText = rows[0].InnerText ?? "";
                    bool isDownloadTable = headerText.Contains("Date added", StringComparison.OrdinalIgnoreCase)
                        || headerText.Contains("File", StringComparison.OrdinalIgnoreCase)
                        || headerText.Contains("Version", StringComparison.OrdinalIgnoreCase);
                    if (!isDownloadTable)
                        continue;
                    var firstDataRow = rows[1];
                    var linkNode = firstDataRow.SelectSingleNode(".//a[contains(@href,'downloads')]")
                        ?? firstDataRow.SelectSingleNode(".//a[contains(@href,'download')]")
                        ?? firstDataRow.SelectSingleNode(".//a[contains(@href,'.zip')]")
                        ?? firstDataRow.SelectSingleNode(".//a[contains(@href,'/file')]");
                    if (linkNode != null)
                    {
                        downloadUrl = linkNode.GetAttributeValue("href", "");
                        if (!string.IsNullOrEmpty(downloadUrl) && !downloadUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                            downloadUrl = new Uri(new Uri(BaseUrl), downloadUrl).ToString();
                    }
                    var cells = firstDataRow.SelectNodes(".//td");
                    if (cells != null && cells.Count >= 2)
                    {
                        var dateStr = cells[1].InnerText?.Trim();
                        if (!string.IsNullOrEmpty(dateStr) && DateTime.TryParse(dateStr, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var dt))
                            lastUpdated = dt;
                    }
                    if (!string.IsNullOrEmpty(downloadUrl)) break;
                }
            }

            // Fallbacks: any table row with a download-like link, then page-wide links
            if (string.IsNullOrEmpty(downloadUrl) && tables != null)
            {
                foreach (var table in tables)
                {
                    var allLinks = table.SelectNodes(".//a[@href]");
                    if (allLinks == null) continue;
                    foreach (HtmlNode a in allLinks)
                    {
                        var href = a.GetAttributeValue("href", "");
                        if (string.IsNullOrEmpty(href)) continue;
                        if (href.Contains("download", StringComparison.OrdinalIgnoreCase) || href.Contains(".zip", StringComparison.OrdinalIgnoreCase) || href.Contains("/file", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!href.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                                href = new Uri(new Uri(BaseUrl), href).ToString();
                            downloadUrl = href;
                            break;
                        }
                    }
                    if (!string.IsNullOrEmpty(downloadUrl)) break;
                }
            }

            if (string.IsNullOrEmpty(downloadUrl))
            {
                var attachmentLinks = doc.DocumentNode.SelectNodes("//a[contains(@href, 'attachment_id')]");
                if (attachmentLinks != null && attachmentLinks.Count > 0)
                    downloadUrl = attachmentLinks[0].GetAttributeValue("href", "");
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    var zipLink = doc.DocumentNode.SelectSingleNode("//a[contains(@href, '.zip')]");
                    if (zipLink != null)
                        downloadUrl = zipLink.GetAttributeValue("href", "");
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    var downloadLinks = doc.DocumentNode.SelectNodes("//article//a[contains(@href, 'download')]");
                    if (downloadLinks != null && downloadLinks.Count > 0)
                        downloadUrl = downloadLinks[0].GetAttributeValue("href", "");
                }
                if (string.IsNullOrEmpty(downloadUrl))
                {
                    var anyDownload = doc.DocumentNode.SelectSingleNode("//a[contains(@href,'/downloads/')]")
                        ?? doc.DocumentNode.SelectSingleNode("//a[contains(@href,'/file/')]");
                    if (anyDownload != null)
                        downloadUrl = anyDownload.GetAttributeValue("href", "");
                }
                if (!string.IsNullOrEmpty(downloadUrl) && !downloadUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    downloadUrl = new Uri(new Uri(BaseUrl), downloadUrl).ToString();
                }
            }

            if (!string.IsNullOrEmpty(downloadUrl))
                downloadUrl = downloadUrl.Trim().TrimEnd(';', ' ');

            if (lastUpdated == null)
            {
                var bodyText = doc.DocumentNode.InnerText ?? "";
                lastUpdated = ParseLastUpdatedFromText(bodyText) ?? DateTime.Now;
            }

            return new Trainer
            {
                Title = title,
                PageUrl = url,
                DownloadUrl = downloadUrl,
                IsDownloaded = false,
                ImageUrl = imageUrl,
                LastUpdated = lastUpdated
            };
        }, $"get details for {url}");
    }
}
