using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using GameTrainerLauncher.Core.Entities;
using GameTrainerLauncher.Infrastructure;

namespace GameTrainerLauncher.UI.Converters;

/// <summary>
/// Takes a Game and returns Visible when it has no local cover file.
/// </summary>
public class GameCoverPlaceholderVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not Game game)
        {
            return Visibility.Visible;
        }

        try
        {
            if (game.Id > 0)
            {
                AppPaths.EnsureCoversFolderExists();
                return Directory.GetFiles(AppPaths.CoversFolder, $"game_{game.Id}.*").Any(File.Exists)
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }
        }
        catch
        {
        }

        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
