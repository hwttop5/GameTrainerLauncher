using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using GameTrainerLauncher.Core.Entities;

namespace GameTrainerLauncher.UI.Converters;

/// <summary>
/// Takes a Game and returns Visible when it has no cover URL (for placeholder icon); otherwise Collapsed.
/// </summary>
public class GameCoverPlaceholderVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Game game) return Visibility.Visible;
        var url = game.MatchedTrainer?.ImageUrl ?? game.CoverUrl;
        return string.IsNullOrWhiteSpace(url) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
