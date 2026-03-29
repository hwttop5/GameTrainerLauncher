using System.Text.RegularExpressions;

namespace GameTrainerLauncher.Core.Utilities;

public static partial class TitleSearchNormalizer
{
    private static readonly HashSet<string> VersionTokens =
    [
        "edition",
        "remastered",
        "definitive",
        "ultimate",
        "complete",
        "collection",
        "collections",
        "directors",
        "director",
        "cut",
        "demo",
        "vr",
        "windows",
        "enhanced",
        "reload",
        "redux",
        "reloaded",
        "anniversary",
        "goty",
        "game",
        "year",
        "standard",
        "special",
        "bundle",
        "pack",
        "dlc",
        "completeedition",
        "bundle",
        "premium",
        "gold",
        "deluxe",
        "expanded",
        "updated",
        "update",
        "early",
        "access",
        "beta",
        "alpha",
        "x64",
        "bit",
        "bits",
        "64",
        "32"
    ];

    public static bool ContainsCjk(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (ch is >= '\u3400' and <= '\u9fff' or >= '\uf900' and <= '\ufaff')
            {
                return true;
            }
        }

        return false;
    }

    public static string RemoveTrainerSuffix(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var trimmed = CollapseWhitespace(title);
        var match = TrainerSuffixRegex().Match(trimmed);
        return match.Success
            ? CollapseWhitespace(match.Groups["prefix"].Value)
            : trimmed;
    }

    public static string NormalizeFlingTitle(string? title)
    {
        return NormalizeEnglishTitle(RemoveTrainerSuffix(title));
    }

    public static string NormalizeEnglishTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var lower = CollapseWhitespace(title).ToLowerInvariant();
        lower = lower.Replace("’", string.Empty).Replace("'", string.Empty);
        lower = NonAlphaNumericRegex().Replace(lower, " ");

        var tokens = lower
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => !VersionTokens.Contains(token));

        return string.Join(' ', tokens);
    }

    public static string NormalizeEnglishTitleKeepVersion(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var lower = CollapseWhitespace(title).ToLowerInvariant();
        lower = lower.Replace("’", string.Empty).Replace("'", string.Empty);
        lower = NonAlphaNumericRegex().Replace(lower, " ");
        var tokens = lower
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return string.Join(' ', tokens);
    }

    public static string NormalizeEnglishMainTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var raw = CollapseWhitespace(title);
        var separators = new[] { " - ", " – ", " — ", ": " };
        foreach (var separator in separators)
        {
            var idx = raw.IndexOf(separator, StringComparison.Ordinal);
            if (idx > 0)
            {
                raw = raw[..idx];
                break;
            }
        }

        return NormalizeEnglishTitle(raw);
    }

    public static bool IsVersionOnlyDifference(string? firstTitle, string? secondTitle)
    {
        var firstMain = NormalizeEnglishMainTitle(firstTitle);
        var secondMain = NormalizeEnglishMainTitle(secondTitle);
        if (string.IsNullOrWhiteSpace(firstMain) || firstMain != secondMain)
        {
            return false;
        }

        var firstRaw = NormalizeEnglishTitleKeepVersion(firstTitle);
        var secondRaw = NormalizeEnglishTitleKeepVersion(secondTitle);
        return !string.IsNullOrWhiteSpace(firstRaw) &&
               !string.IsNullOrWhiteSpace(secondRaw) &&
               firstRaw != secondRaw;
    }

    public static string NormalizeChineseTitle(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        var lower = CollapseWhitespace(title).ToLowerInvariant();
        lower = lower.Replace("’", string.Empty).Replace("'", string.Empty);
        return NonLetterOrDigitRegex().Replace(lower, string.Empty);
    }

    public static HashSet<string> TokenizeEnglish(string? title)
    {
        var normalized = NormalizeEnglishTitle(title);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return [];
        }

        return normalized
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.Ordinal);
    }

    public static string CollapseWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return WhitespaceRegex().Replace(value.Trim(), " ");
    }

    [GeneratedRegex(@"^(?<prefix>.+?)\btrainer\b.*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TrainerSuffixRegex();

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"[^a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonAlphaNumericRegex();

    [GeneratedRegex(@"[^\p{L}\p{Nd}]+", RegexOptions.CultureInvariant)]
    private static partial Regex NonLetterOrDigitRegex();
}
