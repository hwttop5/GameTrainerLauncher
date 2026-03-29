namespace GameTrainerLauncher.Core.Models;

public sealed class GameTitleMetadata
{
    public required string Source { get; set; }
    public required string SourceKey { get; set; }
    public string? SourceUrl { get; set; }
    public string? EnglishName { get; set; }
    public string? ChineseName { get; set; }
}
