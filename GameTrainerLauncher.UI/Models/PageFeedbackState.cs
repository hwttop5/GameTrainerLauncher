using CommunityToolkit.Mvvm.ComponentModel;
using Wpf.Ui.Controls;

namespace GameTrainerLauncher.UI.Models;

public partial class PageFeedbackState : ObservableObject
{
    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private InfoBarSeverity _severity = InfoBarSeverity.Informational;

    public void Show(InfoBarSeverity severity, string title, string message)
    {
        Severity = severity;
        Title = title;
        Message = message;
        IsOpen = true;
    }

    public void Hide()
    {
        IsOpen = false;
        Title = string.Empty;
        Message = string.Empty;
        Severity = InfoBarSeverity.Informational;
    }
}
