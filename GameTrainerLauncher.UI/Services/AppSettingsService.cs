using System.IO;
using System.Text.Json;
using GameTrainerLauncher.Infrastructure;

namespace GameTrainerLauncher.UI.Services;

public sealed class AppSettings
{
    public string Theme { get; set; } = "Dark";
    public string Language { get; set; } = "en-US";
    public DateTimeOffset? LastUpdateCheckUtc { get; set; }
    public string? SkippedVersion { get; set; }

    public AppSettings Clone()
    {
        return new AppSettings
        {
            Theme = Theme,
            Language = Language,
            LastUpdateCheckUtc = LastUpdateCheckUtc,
            SkippedVersion = SkippedVersion
        };
    }
}

public interface IAppSettingsService
{
    AppSettings GetSettings();
    void Update(Action<AppSettings> update);
}

public sealed class AppSettingsService : IAppSettingsService
{
    private readonly object _syncRoot = new();
    private readonly string? _settingsPath;
    private AppSettings _settings = new();

    public AppSettingsService()
    {
        try
        {
            AppPaths.EnsureDataFolderExists();
            _settingsPath = Path.Combine(AppPaths.DataFolder, "settings.json");
            Load();
        }
        catch
        {
            _settingsPath = null;
        }
    }

    public AppSettings GetSettings()
    {
        lock (_syncRoot)
        {
            return _settings.Clone();
        }
    }

    public void Update(Action<AppSettings> update)
    {
        ArgumentNullException.ThrowIfNull(update);

        AppSettings snapshotToSave;
        lock (_syncRoot)
        {
            var next = _settings.Clone();
            update(next);
            _settings = next;
            snapshotToSave = next.Clone();
        }

        Save(snapshotToSave);
    }

    private void Load()
    {
        if (_settingsPath == null || !File.Exists(_settingsPath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var loaded = JsonSerializer.Deserialize<AppSettings>(json);
            if (loaded != null)
            {
                _settings = loaded;
            }
        }
        catch
        {
            _settings = new AppSettings();
        }
    }

    private void Save(AppSettings settings)
    {
        if (_settingsPath == null)
        {
            return;
        }

        try
        {
            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
            // Ignore settings persistence failures.
        }
    }
}
