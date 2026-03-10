using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace GameTrainerLauncher.UI.Converters;

/// <summary>
/// Returns Visible when value is null or empty string; otherwise Collapsed.
/// Use for placeholder (e.g. icon) when no image URL.
/// </summary>
public class NullOrEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isEmpty = value == null || (value is string s && string.IsNullOrWhiteSpace(s));
        return isEmpty ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
