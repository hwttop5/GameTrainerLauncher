using System;
using System.Globalization;
using System.Windows.Data;

namespace GameTrainerLauncher.UI.Converters;

/// <summary>MultiValueConverter: (Trainer, CurrentAddingTrainer). True when this card is the one currently adding (by reference).</summary>
public class IsCurrentAddingConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values == null || values.Length < 2) return false;
        var current = values[1];
        if (current == null || current == System.Windows.DependencyProperty.UnsetValue) return false;
        return ReferenceEquals(values[0], current);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
