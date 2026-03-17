using System;
using System.Globalization;
using System.Windows.Data;
using GameTrainerLauncher.Core.Entities;

namespace GameTrainerLauncher.UI.Converters;

/// <summary>
/// MultiValueConverter: (Game, CurrentDownloadingGameId).
/// Returns true when the download button should be enabled: has trainer, not downloaded, and this row is not the one currently downloading.
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
        var currentGameId = values[1] as int?;
        if (currentGameId == null) return true;
        return game.Id != currentGameId.Value;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
