using System.Windows;
using System.Windows.Media;
using GameTrainerLauncher.UI.Services;

namespace GameTrainerLauncher.UI.Views;

public partial class AppMessageDialogWindow : Wpf.Ui.Controls.FluentWindow
{
    public bool PrimaryAccepted { get; private set; }

    public AppMessageDialogWindow(
        string title,
        string message,
        AppDialogSeverity severity,
        string primaryButtonText,
        string? secondaryButtonText,
        bool primaryIsDanger)
    {
        InitializeComponent();

        Title = title;
        DialogTitleBar.Title = title;
        DialogTitleText.Text = title;
        DialogMessageText.Text = message;

        SeverityIconHost.Background = GetBrush(GetSeverityBrushKey(severity));
        SeverityIconText.Text = GetSeverityGlyph(severity);

        PrimaryButton.Content = primaryButtonText;
        PrimaryButton.Appearance = primaryIsDanger
            ? Wpf.Ui.Controls.ControlAppearance.Danger
            : Wpf.Ui.Controls.ControlAppearance.Primary;

        if (string.IsNullOrWhiteSpace(secondaryButtonText))
        {
            SecondaryButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            SecondaryButton.Content = secondaryButtonText;
            SecondaryButton.Visibility = Visibility.Visible;
        }
    }

    private static Brush GetBrush(string key)
    {
        return Application.Current.FindResource(key) as Brush ?? Brushes.DodgerBlue;
    }

    private static string GetSeverityGlyph(AppDialogSeverity severity) => severity switch
    {
        AppDialogSeverity.Success => "✓",
        AppDialogSeverity.Warning => "!",
        AppDialogSeverity.Error => "×",
        _ => "i"
    };

    private static string GetSeverityBrushKey(AppDialogSeverity severity) => severity switch
    {
        AppDialogSeverity.Success => "StatusSuccessBrush",
        AppDialogSeverity.Warning => "StatusWarningBrush",
        AppDialogSeverity.Error => "StatusErrorBrush",
        _ => "StatusInfoBrush"
    };

    private void PrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        PrimaryAccepted = true;
        DialogResult = true;
        Close();
    }

    private void SecondaryButton_Click(object sender, RoutedEventArgs e)
    {
        PrimaryAccepted = false;
        DialogResult = false;
        Close();
    }
}
