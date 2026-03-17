using System;
using System.Globalization;
using System.Windows.Data;
using GameTrainerLauncher.Core.Entities;

namespace GameTrainerLauncher.UI.Converters;

/// <summary>
/// MultiValueConverter: (Game, CurrentDownloadingGame). Enable when no download in progress or this row is not the one downloading (by reference).
/// </summary>
public class CanDownloadConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2) return false;
        var game = values[0] as Game;
        if (game?.MatchedTrainer == null) return false;
        if (game.MatchedTrainer.IsDownloaded) return false;
        if (values[1] == null || values[1] == System.Windows.DependencyProperty.UnsetValue) return true;
        var current = values[1] as Game;
        if (current == null) return true;
        return !ReferenceEquals(game, current);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
