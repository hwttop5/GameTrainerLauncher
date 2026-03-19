using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using GameTrainerLauncher.Infrastructure;
using System.IO;

namespace GameTrainerLauncher.UI.Converters;

/// <summary>
/// IMultiValueConverter: values[0]=MatchedTrainer.ImageUrl, values[1]=CoverUrl, values[2]=Game.Id.
/// 若本地已有封面，则隐藏占位；否则当 URL 也为空时显示占位。
/// </summary>
public class GameCoverPlaceholderFromPartsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        try
        {
            if (values?.Length > 2 && values[2] is int gameId && gameId > 0)
            {
                AppPaths.EnsureCoversFolderExists();
                var files = Directory.GetFiles(AppPaths.CoversFolder, $"game_{gameId}.*");
                if (files.Length > 0) return Visibility.Collapsed;
            }
        }
        catch { /* ignore */ }

        var hasUrl = (values?.Length > 0 && values[0] is string s0 && !string.IsNullOrWhiteSpace(s0))
            || (values?.Length > 1 && values[1] is string s1 && !string.IsNullOrWhiteSpace(s1));
        return hasUrl ? Visibility.Collapsed : Visibility.Visible;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
