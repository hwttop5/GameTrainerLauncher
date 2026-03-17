using System;
using System.Globalization;
using System.Windows.Data;
using GameTrainerLauncher.Core.Entities;

namespace GameTrainerLauncher.UI.Converters;

/// <summary>
/// MultiValueConverter: (Game, CurrentLaunchingGame). True when this row is the one currently launching (by reference).
/// </summary>
public class IsCurrentLaunchingConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2) return false;
        var game = values[0] as Game;
        if (game?.MatchedTrainer == null) return false;
        var current = values[1] as Game;
        if (current == null || current == System.Windows.DependencyProperty.UnsetValue) return false;
        return ReferenceEquals(game, current);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
