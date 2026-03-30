using System.Diagnostics;
using System.Reflection;
using NLog;
using Velopack;
using Velopack.Sources;

namespace GameTrainerLauncher.UI.Services;

public enum AppUpdateState
{
    NotChecked,
    NotInstalled,
    CoolingDown,
    UpToDate,
    UpdateAvailable,
    SkippedVersion,
    PendingRestart,
    Error
}

public sealed class UpdateCheckResult
{
    internal UpdateInfo? UpdateInfo { get; init; }

    public required AppUpdateState State { get; init; }
    public required string CurrentVersion { get; init; }
    public string? AvailableVersion { get; init; }
    public string? ReleaseNotesMarkdown { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset? CheckedAtUtc { get; init; }
}

public sealed class UpdateStatusSnapshot
{
    public required AppUpdateState State { get; init; }
    public required string CurrentVersion { get; init; }
    public string? AvailableVersion { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTimeOffset? LastCheckedAtUtc { get; init; }
}

public interface IAppUpdateService
{
    UpdateStatusSnapshot GetStatusSnapshot();
    Task<UpdateCheckResult> CheckForUpdatesAsync(bool manual, CancellationToken cancellationToken = default);
    Task<bool> DownloadUpdateAndRestartAsync(UpdateCheckResult result, Action<int>? onProgress = null, CancellationToken cancellationToken = default);
    void SkipVersion(string version);
}

public sealed class AppUpdateService : IAppUpdateService
{
    private const string RepositoryUrl = "https://github.com/hwttop5/GameTrainerLauncher";
    private const string DefaultUpdateFeedBaseUrl = "https://hwttop5.github.io/GameTrainerLauncher/velopack";
    private const string UpdateFeedBaseUrlEnvVar = "GAME_TRAINER_LAUNCHER_UPDATE_FEED_URL";
    private const string UpdateSourceOverrideEnvVar = "GAME_TRAINER_LAUNCHER_UPDATE_SOURCE";
    internal const string SourceUnavailableErrorCode = "UPDATE_FEED_UNAVAILABLE";
    private static readonly TimeSpan AutomaticCheckCooldown = TimeSpan.FromHours(24);
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private readonly IAppSettingsService _settingsService;
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private readonly Lazy<UpdateManager> _updateManagerFactory;
    private UpdateStatusSnapshot _lastStatus;

    public AppUpdateService(IAppSettingsService settingsService)
    {
        _settingsService = settingsService;
        _updateManagerFactory = new Lazy<UpdateManager>(CreateUpdateManager);
        _lastStatus = new UpdateStatusSnapshot
        {
            State = AppUpdateState.NotChecked,
            CurrentVersion = GetCurrentVersionDisplay()
        };
    }

    public UpdateStatusSnapshot GetStatusSnapshot()
    {
        var settings = _settingsService.GetSettings();
        return new UpdateStatusSnapshot
        {
            State = _lastStatus.State,
            CurrentVersion = GetCurrentVersionDisplay(),
            AvailableVersion = _lastStatus.AvailableVersion,
            ErrorMessage = _lastStatus.ErrorMessage,
            LastCheckedAtUtc = settings.LastUpdateCheckUtc
        };
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync(bool manual, CancellationToken cancellationToken = default)
    {
        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            var settings = _settingsService.GetSettings();
            var currentVersion = GetCurrentVersionDisplay();
            var updateManager = _updateManagerFactory.Value;

            if (!manual && settings.LastUpdateCheckUtc.HasValue && DateTimeOffset.UtcNow - settings.LastUpdateCheckUtc.Value < AutomaticCheckCooldown)
            {
                return SetLastStatus(new UpdateCheckResult
                {
                    State = AppUpdateState.CoolingDown,
                    CurrentVersion = currentVersion,
                    CheckedAtUtc = settings.LastUpdateCheckUtc
                });
            }

            if (!updateManager.IsInstalled)
            {
                return SetLastStatus(new UpdateCheckResult
                {
                    State = AppUpdateState.NotInstalled,
                    CurrentVersion = currentVersion
                });
            }

            if (updateManager.UpdatePendingRestart != null)
            {
                return SetLastStatus(new UpdateCheckResult
                {
                    State = AppUpdateState.PendingRestart,
                    CurrentVersion = currentVersion,
                    AvailableVersion = updateManager.UpdatePendingRestart.Version?.ToString(),
                    CheckedAtUtc = settings.LastUpdateCheckUtc
                });
            }

            var updateInfo = await updateManager.CheckForUpdatesAsync();
            var checkedAtUtc = DateTimeOffset.UtcNow;
            _settingsService.Update(next => next.LastUpdateCheckUtc = checkedAtUtc);

            if (updateInfo == null)
            {
                _settingsService.Update(next => next.SkippedVersion = null);
                return SetLastStatus(new UpdateCheckResult
                {
                    State = AppUpdateState.UpToDate,
                    CurrentVersion = currentVersion,
                    CheckedAtUtc = checkedAtUtc
                });
            }

            var availableVersion = updateInfo.TargetFullRelease.Version.ToString();
            if (!manual && !string.IsNullOrWhiteSpace(settings.SkippedVersion) &&
                string.Equals(settings.SkippedVersion, availableVersion, StringComparison.OrdinalIgnoreCase))
            {
                return SetLastStatus(new UpdateCheckResult
                {
                    State = AppUpdateState.SkippedVersion,
                    CurrentVersion = currentVersion,
                    AvailableVersion = availableVersion,
                    ReleaseNotesMarkdown = updateInfo.TargetFullRelease.NotesMarkdown,
                    CheckedAtUtc = checkedAtUtc
                });
            }

            return SetLastStatus(new UpdateCheckResult
            {
                State = AppUpdateState.UpdateAvailable,
                CurrentVersion = currentVersion,
                AvailableVersion = availableVersion,
                ReleaseNotesMarkdown = updateInfo.TargetFullRelease.NotesMarkdown,
                CheckedAtUtc = checkedAtUtc,
                UpdateInfo = updateInfo
            });
        }
        catch (Exception ex)
        {
            Logger.Warn(ex, "Update check failed.");
            var checkedAtUtc = DateTimeOffset.UtcNow;
            _settingsService.Update(next => next.LastUpdateCheckUtc = checkedAtUtc);
            return SetLastStatus(new UpdateCheckResult
            {
                State = AppUpdateState.Error,
                CurrentVersion = GetCurrentVersionDisplay(),
                ErrorMessage = NormalizeUpdateErrorMessage(ex.Message),
                CheckedAtUtc = checkedAtUtc
            });
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public async Task<bool> DownloadUpdateAndRestartAsync(UpdateCheckResult result, Action<int>? onProgress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.UpdateInfo == null)
        {
            return false;
        }

        await _operationLock.WaitAsync(cancellationToken);
        try
        {
            var updateManager = _updateManagerFactory.Value;
            if (!updateManager.IsInstalled)
            {
                return false;
            }

            await updateManager.DownloadUpdatesAsync(result.UpdateInfo, progress => onProgress?.Invoke(progress), cancellationToken);

            var toApply = updateManager.UpdatePendingRestart ?? result.UpdateInfo.TargetFullRelease;
            await updateManager.WaitExitThenApplyUpdatesAsync(toApply, silent: false, restart: true, restartArgs: Array.Empty<string>());
            _settingsService.Update(next => next.SkippedVersion = null);
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to download/apply update.");
            _lastStatus = new UpdateStatusSnapshot
            {
                State = AppUpdateState.Error,
                CurrentVersion = GetCurrentVersionDisplay(),
                ErrorMessage = ex.Message,
                LastCheckedAtUtc = _settingsService.GetSettings().LastUpdateCheckUtc
            };
            return false;
        }
        finally
        {
            _operationLock.Release();
        }
    }

    public void SkipVersion(string version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return;
        }

        _settingsService.Update(next => next.SkippedVersion = version);
        _lastStatus = new UpdateStatusSnapshot
        {
            State = AppUpdateState.SkippedVersion,
            CurrentVersion = GetCurrentVersionDisplay(),
            AvailableVersion = version,
            LastCheckedAtUtc = _settingsService.GetSettings().LastUpdateCheckUtc
        };
    }

    private UpdateManager CreateUpdateManager()
    {
        var sourceOverride = Environment.GetEnvironmentVariable(UpdateSourceOverrideEnvVar);
        if (string.Equals(sourceOverride, "github", StringComparison.OrdinalIgnoreCase))
        {
            Logger.Info("App update source overridden to GithubSource by env var {EnvVar}.", UpdateSourceOverrideEnvVar);
            var source = new GithubSource(RepositoryUrl, accessToken: null, prerelease: false, downloader: null);
            return new UpdateManager(source, new UpdateOptions(), locator: null);
        }

        var feedBaseUrl = Environment.GetEnvironmentVariable(UpdateFeedBaseUrlEnvVar);
        if (string.IsNullOrWhiteSpace(feedBaseUrl))
        {
            feedBaseUrl = DefaultUpdateFeedBaseUrl;
        }

        feedBaseUrl = feedBaseUrl.Trim().TrimEnd('/');
        Logger.Info("Using static update feed: {FeedBaseUrl}", feedBaseUrl);
        return new UpdateManager(feedBaseUrl, new UpdateOptions(), locator: null);
    }

    private static string NormalizeUpdateErrorMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return message;
        }

        if (message.Contains("403", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("404", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("timed out", StringComparison.OrdinalIgnoreCase))
        {
            return SourceUnavailableErrorCode;
        }

        return message;
    }

    private UpdateCheckResult SetLastStatus(UpdateCheckResult result)
    {
        _lastStatus = new UpdateStatusSnapshot
        {
            State = result.State,
            CurrentVersion = result.CurrentVersion,
            AvailableVersion = result.AvailableVersion,
            ErrorMessage = result.ErrorMessage,
            LastCheckedAtUtc = result.CheckedAtUtc ?? _settingsService.GetSettings().LastUpdateCheckUtc
        };

        return result;
    }

    private string GetCurrentVersionDisplay()
    {
        if (_updateManagerFactory.IsValueCreated && _updateManagerFactory.Value.CurrentVersion != null)
        {
            return NormalizeVersion(_updateManagerFactory.Value.CurrentVersion.ToString());
        }

        var informationalVersion = Assembly.GetEntryAssembly()
            ?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return NormalizeVersion(informationalVersion);
        }

        var fileVersion = FileVersionInfo.GetVersionInfo(Environment.ProcessPath ?? string.Empty).ProductVersion;
        if (!string.IsNullOrWhiteSpace(fileVersion))
        {
            return NormalizeVersion(fileVersion);
        }

        return "0.0.0";
    }

    private static string NormalizeVersion(string version)
    {
        var plusIndex = version.IndexOf('+');
        return plusIndex >= 0 ? version[..plusIndex] : version;
    }
}
