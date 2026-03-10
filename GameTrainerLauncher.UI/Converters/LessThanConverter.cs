using System;
using System.Globalization;
using System.Windows.Data;

namespace GameTrainerLauncher.UI.Converters
{
    public class LessThanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double v && parameter != null && double.TryParse(parameter.ToString(), out double p))
            {
                return v < p;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}