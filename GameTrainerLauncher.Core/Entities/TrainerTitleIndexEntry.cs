namespace GameTrainerLauncher.Core.Entities;

public class TrainerTitleIndexEntry
{
    public const string MatchStatusMatched = "Matched";
    public const string MatchStatusUnmatched = "Unmatched";
    public const string MetadataSourceGamersky = "Gamersky";
    public const string MetadataSourceSteam = "Steam";

    public int Id { get; set; }
    public required string FlingTitle { get; set; }
    public required string TrainerPageUrl { get; set; }
    public string? SteamAppId { get; set; }
    public string? MetadataSource { get; set; }
    public string? MetadataSourceUrl { get; set; }
    public string? EnglishName { get; set; }
    public string? ChineseName { get; set; }
    public required string NormalizedFlingTitle { get; set; }
    public string? NormalizedEnglishName { get; set; }
    public string? NormalizedChineseName { get; set; }
    public string MatchStatus { get; set; } = MatchStatusUnmatched;
    public double? MatchConfidence { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? LastSyncedUtc { get; set; }
    public DateTime? LastSeenUtc { get; set; }
    public DateTime? LastValidatedUtc { get; set; }
}
