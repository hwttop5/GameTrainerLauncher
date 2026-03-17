using System;
using System.Globalization;
using System.Windows.Data;
using GameTrainerLauncher.Core.Entities;

namespace GameTrainerLauncher.UI.Converters;

/// <summary>
/// MultiValueConverter: (Game, CurrentDownloadingGameId).
/// Returns true when this row is the one currently downloading (show progress bar).
/// </summary>
public class IsCurrentDownloadingConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2) return false;
        var game = values[0] as Game;
        if (game?.MatchedTrainer == null) return false;
        if (values[1] == null || values[1] == System.Windows.DependencyProperty.UnsetValue) return false;
        var currentGameId = values[1] as int?;
        if (currentGameId == null) return false;
        return game.Id == currentGameId.Value;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
