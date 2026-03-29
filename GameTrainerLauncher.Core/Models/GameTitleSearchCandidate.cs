namespace GameTrainerLauncher.Core.Models;

public sealed class GameTitleSearchCandidate
{
    public required string SourceKey { get; set; }
    public required string DetailUrl { get; set; }
    public required string Title { get; set; }
}
