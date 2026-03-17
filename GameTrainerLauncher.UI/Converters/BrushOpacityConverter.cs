using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace GameTrainerLauncher.UI.Converters;

public class BrushOpacityConverter : IValueConverter
{
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Brush brush) return null;
        if (!double.TryParse(parameter?.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var opacity))
            opacity = 0.5;
        opacity = Math.Clamp(opacity, 0, 1);

        var clone = brush.Clone();
        clone.Opacity = opacity;
        clone.Freeze();
        return clone;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

