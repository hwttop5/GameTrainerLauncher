using System.Globalization;
using System.Windows;

namespace GameTrainerLauncher.UI.Services;

internal static class UpdateTextFormatter
{
    public static string GetStatusText(UpdateStatusSnapshot snapshot)
    {
        if (string.Equals(snapshot.ErrorMessage, AppUpdateService.SourceUnavailableErrorCode, StringComparison.Ordinal))
        {
            return GetString("UpdateStatusSourceUnavailable");
        }

        return snapshot.State switch
        {
            AppUpdateState.NotInstalled => GetString("UpdateStatusNotInstalled"),
            AppUpdateState.CoolingDown => GetString("UpdateStatusCoolingDown"),
            AppUpdateState.UpToDate => GetString("UpdateStatusUpToDate"),
            AppUpdateState.UpdateAvailable => Format("UpdateStatusAvailable", snapshot.AvailableVersion ?? "?"),
            AppUpdateState.SkippedVersion => Format("UpdateStatusSkipped", snapshot.AvailableVersion ?? "?"),
            AppUpdateState.PendingRestart => GetString("UpdateStatusPendingRestart"),
            AppUpdateState.Error => Format("UpdateStatusError", snapshot.ErrorMessage ?? GetString("MsgErrorTitle")),
            _ => GetString("UpdateStatusNotChecked")
        };
    }

    public static string GetManualCheckMessage(UpdateCheckResult result)
    {
        if (string.Equals(result.ErrorMessage, AppUpdateService.SourceUnavailableErrorCode, StringComparison.Ordinal))
        {
            return GetString("UpdateManualSourceUnavailable");
        }

        return result.State switch
        {
            AppUpdateState.UpToDate => GetString("UpdateManualNoUpdate"),
            AppUpdateState.PendingRestart => GetString("UpdateManualPendingRestart"),
            AppUpdateState.NotInstalled => GetString("UpdateManualNotInstalled"),
            AppUpdateState.Error => Format("UpdateManualError", result.ErrorMessage ?? GetString("MsgErrorTitle")),
            _ => GetStatusText(new UpdateStatusSnapshot
            {
                State = result.State,
                CurrentVersion = result.CurrentVersion,
                AvailableVersion = result.AvailableVersion,
                ErrorMessage = result.ErrorMessage,
                LastCheckedAtUtc = result.CheckedAtUtc
            })
        };
    }

    public static string FormatLastChecked(DateTimeOffset? checkedAtUtc)
    {
        if (!checkedAtUtc.HasValue)
        {
            return GetString("UpdateNeverChecked");
        }

        return checkedAtUtc.Value.LocalDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.CurrentCulture);
    }

    public static string GetString(string key)
    {
        return Application.Current.TryFindResource(key) as string ?? key;
    }

    public static string Format(string key, params object[] args)
    {
        return string.Format(CultureInfo.CurrentCulture, GetString(key), args);
    }
}
