using System.Windows;
using GameTrainerLauncher.UI.Services;

namespace GameTrainerLauncher.UI.Views;

public enum UpdatePromptAction
{
    RemindLater,
    SkipVersion,
    UpdateNow
}

public partial class UpdatePromptWindow : Window
{
    public UpdatePromptAction SelectedAction { get; private set; } = UpdatePromptAction.RemindLater;

    public UpdatePromptWindow(UpdateCheckResult result)
    {
        InitializeComponent();
        SummaryText.Text = UpdateTextFormatter.Format("UpdateDialogSummary", result.CurrentVersion, result.AvailableVersion ?? "?");
        ReleaseNotesTextBox.Text = string.IsNullOrWhiteSpace(result.ReleaseNotesMarkdown)
            ? UpdateTextFormatter.GetString("UpdateNoReleaseNotes")
            : result.ReleaseNotesMarkdown;
    }

    private void UpdateNow_Click(object sender, RoutedEventArgs e)
    {
        SelectedAction = UpdatePromptAction.UpdateNow;
        DialogResult = true;
        Close();
    }

    private void RemindLater_Click(object sender, RoutedEventArgs e)
    {
        SelectedAction = UpdatePromptAction.RemindLater;
        DialogResult = false;
        Close();
    }

    private void SkipVersion_Click(object sender, RoutedEventArgs e)
    {
        SelectedAction = UpdatePromptAction.SkipVersion;
        DialogResult = false;
        Close();
    }
}
