namespace GameTrainerLauncher.Core.Models;

public class TrainerDownloadOption
{
    public required string Label { get; init; }

    public required string DownloadUrl { get; init; }

    public DateTime? PublishedAt { get; init; }

    public string? FileSizeText { get; init; }

    public int SortOrder { get; init; }
}
