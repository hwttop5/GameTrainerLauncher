using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using GameTrainerLauncher.Core.Entities;

namespace GameTrainerLauncher.UI.Converters;

/// <summary>
/// Takes a local file path (or file URI) and returns ImageSource, or null when the image is not local.
/// </summary>
public class GameCoverUrlConverter : IValueConverter
{
    private const int DefaultDecodePixelWidth = 256;

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var decodePixelWidth = GetDecodePixelWidth(parameter);
        var imagePath = value switch
        {
            Game game => game.MatchedTrainer?.CoverImagePath,
            string text => text,
            _ => null
        };

        if (!TryGetLocalImagePath(imagePath, out var localPath))
        {
            return null;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            if (decodePixelWidth > 0)
            {
                image.DecodePixelWidth = decodePixelWidth;
            }
            image.UriSource = new Uri(localPath, UriKind.Absolute);
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
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

    private static bool TryGetLocalImagePath(string? value, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        try
        {
            var candidate = value.Trim();
            if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri) && uri.IsFile)
            {
                candidate = uri.LocalPath;
            }

            if (!Path.IsPathRooted(candidate) || !File.Exists(candidate))
            {
                return false;
            }

            path = candidate;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
