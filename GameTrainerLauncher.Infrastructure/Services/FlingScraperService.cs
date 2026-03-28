using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Interfaces;
using GameTrainerLauncher.Core.Models;
using HtmlAgilityPack;
using NLog;
using System.Globalization;
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
        if (DateTime.TryParseExact(match.Groups[1].Value, "yyyy.MM.dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
            return dt;
        return null;
    }

    private static bool IsDownloadTable(HtmlNode table)
    {
        var headerText = table.InnerText ?? string.Empty;
        return headerText.Contains("Date added", StringComparison.OrdinalIgnoreCase)
            || headerText.Contains("File", StringComparison.OrdinalIgnoreCase)
            || headerText.Contains("Version", StringComparison.OrdinalIgnoreCase);
    }

    private static DateTime? TryParsePublishedAt(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = HtmlEntity.DeEntitize(value).Trim();
        var formats = new[]
        {
            "yyyy-MM-dd HH:mm",
            "yyyy-MM-dd H:mm",
            "yyyy.MM.dd",
            "yyyy-MM-dd"
        };

        if (DateTime.TryParseExact(trimmed, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact))
        {
            return exact;
        }

        if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static string? NormalizeDownloadUrl(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        var normalized = href.Trim().TrimEnd(';', ' ');
        if (!normalized.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            normalized = new Uri(new Uri(BaseUrl), normalized).ToString();
        }

        return normalized;
    }

    private static bool IsSectionLabelRow(HtmlNode row, out string sectionLabel)
    {
        sectionLabel = string.Empty;

        var cells = row.SelectNodes(".//td|.//th");
        if (cells == null || cells.Count != 1)
        {
            return false;
        }

        if (row.SelectSingleNode(".//a[@href]") != null)
        {
            return false;
        }

        sectionLabel = HtmlEntity.DeEntitize(cells[0].InnerText).Trim();
        return !string.IsNullOrWhiteSpace(sectionLabel);
    }

    private static TrainerDownloadOption? ParseDownloadOptionFromRow(HtmlNode row, string fallbackLabel, int sortOrder)
    {
        var linkNode = row.SelectSingleNode("./td[1]//a[@href]")
            ?? row.SelectSingleNode(".//a[@href]");
        if (linkNode == null)
        {
            return null;
        }

        var downloadUrl = NormalizeDownloadUrl(linkNode.GetAttributeValue("href", string.Empty));
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            return null;
        }

        var cells = row.SelectNodes(".//td");
        var label = HtmlEntity.DeEntitize(linkNode.InnerText).Trim();
        if (string.IsNullOrWhiteSpace(label) && cells is { Count: > 0 })
        {
            label = HtmlEntity.DeEntitize(cells[0].InnerText).Trim();
        }

        if (string.IsNullOrWhiteSpace(label))
        {
            label = fallbackLabel;
        }

        return new TrainerDownloadOption
        {
            Label = label,
            DownloadUrl = downloadUrl,
            PublishedAt = cells is { Count: > 1 } ? TryParsePublishedAt(cells[1].InnerText) : null,
            FileSizeText = cells is { Count: > 2 } ? HtmlEntity.DeEntitize(cells[2].InnerText).Trim() : null,
            SortOrder = sortOrder
        };
    }

    private static List<TrainerDownloadOption> ExtractDownloadOptions(HtmlDocument doc, string fallbackLabel)
    {
        var tables = doc.DocumentNode.SelectNodes("//table");
        if (tables == null)
        {
            return [];
        }

        foreach (var table in tables)
        {
            if (!IsDownloadTable(table))
            {
                continue;
            }

            var rows = table.SelectNodes("./tbody/tr") ?? table.SelectNodes(".//tr");
            if (rows == null || rows.Count == 0)
            {
                continue;
            }

            var standaloneOptions = new List<TrainerDownloadOption>();
            var fallbackOptions = new List<TrainerDownloadOption>();
            var currentSectionLabel = string.Empty;

            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var row = rows[rowIndex];

                if (IsSectionLabelRow(row, out var sectionLabel))
                {
                    currentSectionLabel = sectionLabel;
                    continue;
                }

                var option = ParseDownloadOptionFromRow(row, fallbackLabel, fallbackOptions.Count);
                if (option != null)
                {
                    fallbackOptions.Add(option);

                    if (currentSectionLabel.Contains("Standalone", StringComparison.OrdinalIgnoreCase))
                    {
                        standaloneOptions.Add(new TrainerDownloadOption
                        {
                            Label = option.Label,
                            DownloadUrl = option.DownloadUrl,
                            PublishedAt = option.PublishedAt,
                            FileSizeText = option.FileSizeText,
                            SortOrder = standaloneOptions.Count
                        });
                    }
                }
            }

            if (standaloneOptions.Count > 0)
            {
                return standaloneOptions;
            }

            if (fallbackOptions.Count > 0)
            {
                return fallbackOptions;
            }
        }

        return [];
    }

    private static string? ExtractFallbackDownloadUrl(HtmlDocument doc)
    {
        var attachmentLinks = doc.DocumentNode.SelectNodes("//a[contains(@href, 'attachment_id')]");
        if (attachmentLinks != null && attachmentLinks.Count > 0)
        {
            return NormalizeDownloadUrl(attachmentLinks[0].GetAttributeValue("href", string.Empty));
        }

        var zipLink = doc.DocumentNode.SelectSingleNode("//a[contains(@href, '.zip')]");
        if (zipLink != null)
        {
            return NormalizeDownloadUrl(zipLink.GetAttributeValue("href", string.Empty));
        }

        var downloadLinks = doc.DocumentNode.SelectNodes("//article//a[contains(@href, 'download')]");
        if (downloadLinks != null && downloadLinks.Count > 0)
        {
            return NormalizeDownloadUrl(downloadLinks[0].GetAttributeValue("href", string.Empty));
        }

        var anyDownload = doc.DocumentNode.SelectSingleNode("//a[contains(@href,'/downloads/')]")
            ?? doc.DocumentNode.SelectSingleNode("//a[contains(@href,'/file/')]");
        return anyDownload == null
            ? null
            : NormalizeDownloadUrl(anyDownload.GetAttributeValue("href", string.Empty));
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

            var downloadOptions = ExtractDownloadOptions(doc, title);
            if (downloadOptions.Count == 0)
            {
                var fallbackDownloadUrl = ExtractFallbackDownloadUrl(doc);
                if (!string.IsNullOrWhiteSpace(fallbackDownloadUrl))
                {
                    downloadOptions.Add(new TrainerDownloadOption
                    {
                        Label = title,
                        DownloadUrl = fallbackDownloadUrl,
                        PublishedAt = null,
                        FileSizeText = null,
                        SortOrder = 0
                    });
                }
            }

            var selectedOption = downloadOptions.OrderBy(option => option.SortOrder).FirstOrDefault();
            var downloadUrl = selectedOption?.DownloadUrl;
            var version = selectedOption?.Label;
            DateTime? lastUpdated = selectedOption?.PublishedAt;

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
                Version = version,
                IsDownloaded = false,
                ImageUrl = imageUrl,
                LastUpdated = lastUpdated,
                DownloadOptions = downloadOptions
            };
        }, $"get details for {url}");
    }
}
