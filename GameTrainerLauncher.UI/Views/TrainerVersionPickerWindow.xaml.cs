using System.Windows;
using GameTrainerLauncher.Core.Models;
using GameTrainerLauncher.Core.Utilities;
using Wpf.Ui.Controls;

namespace GameTrainerLauncher.UI.Views;

public partial class TrainerVersionPickerWindow : FluentWindow
{
    public TrainerDownloadOption? SelectedOption => OptionsListView.SelectedItem as TrainerDownloadOption;

    public TrainerVersionPickerWindow(string trainerTitle, IReadOnlyList<TrainerDownloadOption> options, string? selectedVersion)
    {
        InitializeComponent();

        SummaryText.Text = string.Format(GetString("TrainerVersionDialogSummary"), trainerTitle);
        OptionsListView.ItemsSource = options.OrderBy(option => option.SortOrder).ToList();
        OptionsListView.SelectedItem = TrainerSelectionHelpers.FindMatchingOption(options, selectedVersion)
            ?? options.OrderBy(option => option.SortOrder).FirstOrDefault();
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (SelectedOption == null)
        {
            return;
        }

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private static string GetString(string key)
    {
        return Application.Current.FindResource(key) as string ?? key;
    }
}
