using GameTrainerLauncher.Core.Entities;

namespace GameTrainerLauncher.Core.Models;

public sealed class TrainerSearchResult
{
    public IReadOnlyList<Trainer> Trainers { get; init; } = [];
    public bool IndexMayBeIncomplete { get; init; }
    public bool IsIndexUpdating { get; init; }
}
