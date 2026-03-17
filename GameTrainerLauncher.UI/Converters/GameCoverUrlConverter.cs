using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using GameTrainerLauncher.Core.Entities;

namespace GameTrainerLauncher.UI.Converters;

/// <summary>
/// Takes a Game and returns ImageSource for display cover (MatchedTrainer?.ImageUrl ?? CoverUrl), or null when empty.
/// </summary>
public class GameCoverUrlConverter : IValueConverter
{
    private const string FlingBaseUrl = "https://flingtrainer.com";

    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Game game)
            return null;
        var url = game.MatchedTrainer?.ImageUrl ?? game.CoverUrl;
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

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
