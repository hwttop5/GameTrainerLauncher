namespace GameTrainerLauncher.Core.Models;

public sealed class SteamAppMetadata
{
    public required string AppId { get; init; }
    public string? EnglishName { get; init; }
    public string? ChineseName { get; init; }
}
