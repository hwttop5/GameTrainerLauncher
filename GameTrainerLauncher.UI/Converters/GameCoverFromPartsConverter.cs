using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using GameTrainerLauncher.Infrastructure;

namespace GameTrainerLauncher.UI.Converters;

public class GameCoverFromPartsConverter : IMultiValueConverter
{
    private const int DefaultDecodePixelWidth = 320;

    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        var decodePixelWidth = GetDecodePixelWidth(parameter);

        try
        {
            if (values?.Length > 2 && values[2] is int gameId && gameId > 0)
            {
                if (GameCoverCache.TryGetCoverPath(gameId, out var firstFile))
                {
                    return CreateBitmapImage(new Uri(firstFile, UriKind.Absolute), decodePixelWidth);
                }
            }
        }
        catch { /* ignore */ }

        return null;
    }

    private static BitmapImage CreateBitmapImage(Uri uri, int decodePixelWidth)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        if (decodePixelWidth > 0)
        {
            image.DecodePixelWidth = decodePixelWidth;
        }
        image.UriSource = uri;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static int GetDecodePixelWidth(object? parameter)
    {
        if (parameter is int value && value > 0)
        {
            return value;
        }

        if (parameter is string text &&
            int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) &&
            parsed > 0)
        {
            return parsed;
        }

        return DefaultDecodePixelWidth;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
