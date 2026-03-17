using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace GameTrainerLauncher.UI.Converters;

/// <summary>
/// IMultiValueConverter: values[0]=MatchedTrainer.ImageUrl, values[1]=CoverUrl.
/// Returns BitmapImage from the first non-empty URL so that when MatchedTrainer.ImageUrl updates (e.g. backfill), the binding refreshes.
/// </summary>
public class GameCoverFromPartsConverter : IMultiValueConverter
{
    private const string FlingBaseUrl = "https://flingtrainer.com";

    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var url = (values?.Length > 0 && values[0] is string s0 && !string.IsNullOrWhiteSpace(s0))
            ? s0
            : (values?.Length > 1 && values[1] is string s1 && !string.IsNullOrWhiteSpace(s1) ? s1 : null);
        if (string.IsNullOrWhiteSpace(url)) return null;
        try
        {
            if (url.StartsWith("/", StringComparison.Ordinal))
                url = FlingBaseUrl + url;
            else if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                url = FlingBaseUrl + "/" + url.TrimStart('/');
            return new BitmapImage(new Uri(url, UriKind.Absolute));
        }
        catch
        {
            return null;
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
