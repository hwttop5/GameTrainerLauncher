using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media.Imaging;
using GameTrainerLauncher.Infrastructure;
using System.IO;

namespace GameTrainerLauncher.UI.Converters;

/// <summary>
/// IMultiValueConverter: values[0]=MatchedTrainer.ImageUrl, values[1]=CoverUrl, values[2]=Game.Id.
/// 优先读取本地封面（%LocalAppData%\GameTrainerLauncher\Data\Covers\game_{id}.*），若不存在则尝试用 URL（但「我的游戏」页会确保缺失时先下载到本地）。
/// Uses BitmapCacheOption.OnLoad so cover displays in installed app (no lazy decode/cache under Program Files).
/// </summary>
public class GameCoverFromPartsConverter : IMultiValueConverter
{
    private const string FlingBaseUrl = "https://flingtrainer.com";

    public object? Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        // 1) 本地封面优先
        try
        {
            if (values?.Length > 2 && values[2] is int gameId && gameId > 0)
            {
                AppPaths.EnsureCoversFolderExists();
                var files = Directory.GetFiles(AppPaths.CoversFolder, $"game_{gameId}.*");
                var firstFile = files.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(firstFile) && File.Exists(firstFile))
                {
                    var imgLocal = new BitmapImage();
                    imgLocal.BeginInit();
                    imgLocal.CacheOption = BitmapCacheOption.OnLoad;
                    imgLocal.UriSource = new Uri(firstFile, UriKind.Absolute);
                    imgLocal.EndInit();
                    return imgLocal;
                }
            }
        }
        catch { /* ignore */ }

        // 2) 回退到 URL
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
            var uri = new Uri(url, UriKind.Absolute);
            var img = new BitmapImage();
            img.BeginInit();
            img.CacheOption = BitmapCacheOption.OnLoad;
            img.UriSource = uri;
            img.EndInit();
            return img;
        }
        catch
        {
            return null;
        }
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
