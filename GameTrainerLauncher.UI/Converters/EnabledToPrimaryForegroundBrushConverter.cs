using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace GameTrainerLauncher.UI.Converters;

public class EnabledToPrimaryForegroundBrushConverter : IValueConverter
{
    private static readonly SolidColorBrush EnabledBrush = new(Colors.White) { Opacity = 1 };
    private static readonly SolidColorBrush DisabledBrush = new(Color.FromRgb(0xA0, 0xA0, 0xA0)) { Opacity = 1 };

    static EnabledToPrimaryForegroundBrushConverter()
    {
        EnabledBrush.Freeze();
        DisabledBrush.Freeze();
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && b ? EnabledBrush : DisabledBrush;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

