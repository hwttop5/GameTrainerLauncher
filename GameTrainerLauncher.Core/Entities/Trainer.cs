using CommunityToolkit.Mvvm.ComponentModel;

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
    public string? ImageUrl { get; set; }
    public DateTime? LastUpdated { get; set; }
    
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
    private bool _isLoading;
}
