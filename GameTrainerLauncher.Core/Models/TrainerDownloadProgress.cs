namespace GameTrainerLauncher.Core.Models;

public sealed record TrainerDownloadProgress(
    TrainerDownloadStage Stage,
    double Percent,
    long BytesReceived,
    long? BytesTotal,
    bool IsEstimated,
    string StatusText);
