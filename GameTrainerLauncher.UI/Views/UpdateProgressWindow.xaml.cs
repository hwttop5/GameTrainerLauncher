using System.ComponentModel;
using System.Windows;
using GameTrainerLauncher.UI.Services;

namespace GameTrainerLauncher.UI.Views;

public partial class UpdateProgressWindow : Window
{
    private bool _allowClose;

    public UpdateProgressWindow()
    {
        InitializeComponent();
    }

    public void SetProgress(int progress)
    {
        if (DownloadProgressBar.IsIndeterminate)
        {
            DownloadProgressBar.IsIndeterminate = false;
        }

        DownloadProgressBar.Value = Math.Clamp(progress, 0, 100);
        StatusText.Text = UpdateTextFormatter.Format("UpdateDownloadingProgressMessage", DownloadProgressBar.Value);
    }

    public void SetMessage(string message)
    {
        StatusText.Text = message;
    }

    public void AllowClose()
    {
        _allowClose = true;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_allowClose)
        {
            e.Cancel = true;
            return;
        }

        base.OnClosing(e);
    }
}
