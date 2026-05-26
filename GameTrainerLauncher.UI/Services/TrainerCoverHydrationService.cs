using System.Windows;
using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Core.Interfaces;
using NLog;

namespace GameTrainerLauncher.UI.Services;

public class TrainerCoverHydrationService
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private const int MaxConcurrentCoverDownloads = 5;

    private readonly IGameCoverService _coverService;

    public TrainerCoverHydrationService(IGameCoverService coverService)
    {
        _coverService = coverService;
    }

    public async Task HydrateAsync(
        IReadOnlyList<Trainer> trainers,
        Func<bool> isCurrent,
        CancellationToken token = default)
    {
        if (trainers.Count == 0 || !isCurrent())
        {
            return;
        }

        using var gate = new SemaphoreSlim(MaxConcurrentCoverDownloads, MaxConcurrentCoverDownloads);
        var tasks = trainers
            .Select(trainer => HydrateOneAsync(trainer, isCurrent, gate, token))
            .ToArray();

        try
        {
            await Task.WhenAll(tasks).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    private async Task HydrateOneAsync(
        Trainer trainer,
        Func<bool> isCurrent,
        SemaphoreSlim gate,
        CancellationToken token)
    {
        try
        {
            if (!isCurrent() || token.IsCancellationRequested)
            {
                return;
            }

            if (_coverService.TryGetTrainerCoverPath(trainer.PageUrl, trainer.ImageUrl, out var cachedPath))
            {
                await UpdateCoverPathAsync(trainer, cachedPath, isCurrent, token).ConfigureAwait(false);
                return;
            }

            if (string.IsNullOrWhiteSpace(trainer.ImageUrl))
            {
                return;
            }

            await gate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (!isCurrent() || token.IsCancellationRequested)
                {
                    return;
                }

                var coverPath = await _coverService
                    .EnsureTrainerCoverAsync(trainer.PageUrl, trainer.ImageUrl, token)
                    .ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(coverPath))
                {
                    await UpdateCoverPathAsync(trainer, coverPath, isCurrent, token).ConfigureAwait(false);
                }
            }
            finally
            {
                gate.Release();
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Trainer cover hydration failed for PageUrl={PageUrl}, ImageUrl={ImageUrl}", trainer.PageUrl, trainer.ImageUrl);
        }
    }

    private static async Task UpdateCoverPathAsync(
        Trainer trainer,
        string coverPath,
        Func<bool> isCurrent,
        CancellationToken token)
    {
        if (!isCurrent() || token.IsCancellationRequested || string.IsNullOrWhiteSpace(coverPath))
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            trainer.CoverImagePath = coverPath;
            return;
        }

        await dispatcher.InvokeAsync(() =>
        {
            if (isCurrent() && !token.IsCancellationRequested)
            {
                trainer.CoverImagePath = coverPath;
            }
        });
    }
}
