using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Models;

namespace GameTrainerLauncher.Core.Utilities;

public static class TrainerSelectionHelpers
{
    public static TrainerDownloadOption CreateFallbackOption(Trainer trainer)
    {
        return new TrainerDownloadOption
        {
            Label = string.IsNullOrWhiteSpace(trainer.Version) ? trainer.Title : trainer.Version,
            DownloadUrl = trainer.DownloadUrl ?? string.Empty,
            PublishedAt = trainer.LastUpdated,
            FileSizeText = null,
            SortOrder = 0
        };
    }

    public static void ApplyDownloadOption(Trainer trainer, TrainerDownloadOption option)
    {
        ArgumentNullException.ThrowIfNull(trainer);
        ArgumentNullException.ThrowIfNull(option);

        trainer.DownloadUrl = option.DownloadUrl;
        trainer.Version = option.Label;

        if (option.PublishedAt != null)
        {
            trainer.LastUpdated = option.PublishedAt;
        }
    }

    public static TrainerDownloadOption? FindMatchingOption(IEnumerable<TrainerDownloadOption>? options, string? versionLabel)
    {
        if (options == null || string.IsNullOrWhiteSpace(versionLabel))
        {
            return null;
        }

        return options.FirstOrDefault(option =>
            string.Equals(option.Label.Trim(), versionLabel.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
