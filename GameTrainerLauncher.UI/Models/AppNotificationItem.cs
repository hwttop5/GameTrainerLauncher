using System.Windows.Media;

namespace GameTrainerLauncher.UI.Models;

public sealed class AppNotificationItem
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public required string IconGlyph { get; init; }
    public required Brush AccentBrush { get; init; }
}
