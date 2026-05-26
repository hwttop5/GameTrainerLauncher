using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using GameTrainerLauncher.Infrastructure;

namespace GameTrainerLauncher.UI.Converters;

public class GameCoverPlaceholderFromPartsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (values?.Length > 2 && values[2] is int gameId && gameId > 0)
            {
                if (GameCoverCache.TryGetCoverPath(gameId, out _))
                {
                    return Visibility.Collapsed;
                }
            }
        }
        catch { /* ignore */ }

        return Visibility.Visible;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
