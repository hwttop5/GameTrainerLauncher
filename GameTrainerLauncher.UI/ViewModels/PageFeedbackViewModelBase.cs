using CommunityToolkit.Mvvm.ComponentModel;
using GameTrainerLauncher.UI.Models;
using Wpf.Ui.Controls;

namespace GameTrainerLauncher.UI.ViewModels;

public abstract class PageFeedbackViewModelBase : ObservableObject
{
    public PageFeedbackState PageFeedback { get; } = new();

    protected void ShowPageFeedback(InfoBarSeverity severity, string title, string message)
    {
        PageFeedback.Show(severity, title, message);
    }

    protected void ClearPageFeedback()
    {
        PageFeedback.Hide();
    }
}
