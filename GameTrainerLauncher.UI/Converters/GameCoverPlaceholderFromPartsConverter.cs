using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GameTrainerLauncher.UI.Converters;

/// <summary>
/// IMultiValueConverter: values[0]=MatchedTrainer.ImageUrl, values[1]=CoverUrl.
/// Returns Visible when both are empty (show placeholder); otherwise Collapsed.
/// </summary>
public class GameCoverPlaceholderFromPartsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var hasUrl = (values?.Length > 0 && values[0] is string s0 && !string.IsNullOrWhiteSpace(s0))
            || (values?.Length > 1 && values[1] is string s1 && !string.IsNullOrWhiteSpace(s1));
        return hasUrl ? Visibility.Collapsed : Visibility.Visible;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
