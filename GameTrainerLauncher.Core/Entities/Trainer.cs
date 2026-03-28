using CommunityToolkit.Mvvm.ComponentModel;
using GameTrainerLauncher.Core.Models;

namespace GameTrainerLauncher.Core.Entities;

public partial class Trainer : ObservableObject
{
    public int Id { get; set; }
    public required string Title { get; set; }
    public required string PageUrl { get; set; }
    public string? DownloadUrl { get; set; }
    public string? LocalZipPath { get; set; }
    public string? LocalExePath { get; set; }
    public string? Version { get; set; }

    [ObservableProperty]
    private string? _imageUrl;

    [ObservableProperty]
    private DateTime? _lastUpdated;

    [ObservableProperty]
    private bool _isDownloaded;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    [ObservableProperty]
    private double _downloadProgress;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    [ObservableProperty]
    private bool _isDownloading;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    [ObservableProperty]
    private string? _downloadStatusText;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    [ObservableProperty]
    private bool _isDownloadProgressEstimated;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    [ObservableProperty]
    private TrainerDownloadStage _downloadStage;

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>True while this trainer is being added to My Games (download in progress). Per-card state for multiple simultaneous adds.</summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    [ObservableProperty]
    private bool _isAdding;
}
