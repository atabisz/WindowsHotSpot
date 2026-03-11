// ConfigManager: loads/saves AppSettings as JSON, fires SettingsChanged on save.
// Pitfall 2: corrupt/missing file falls back to new AppSettings() (never throws to caller).
// Pitfall 3: on load, if StartWithWindows=true, refreshes registry path via StartupManager.RefreshPath().

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WindowsHotSpot.Config;

internal sealed class ConfigManager
{
    private static readonly string SettingsPath =
        Path.Combine(AppContext.BaseDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public AppSettings Settings { get; private set; } = new();

    public event Action? SettingsChanged;

    /// <summary>
    /// Loads settings from settings.json. Falls back to defaults on missing or corrupt file.
    /// If StartWithWindows is true, refreshes the registry path (handles exe move).
    /// </summary>
    public void Load()
    {
        if (File.Exists(SettingsPath))
        {
            try
            {
                string json = File.ReadAllText(SettingsPath);
                AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null)
                    Settings = loaded;
            }
            catch
            {
                // Corrupt file: silently fall back to defaults (Pitfall 2)
                Settings = new AppSettings();
            }
        }

        // Fix stale registry path after exe was moved (Pitfall 3)
        if (Settings.StartWithWindows)
            StartupManager.RefreshPath();
    }

    /// <summary>
    /// Serializes current settings to settings.json and fires SettingsChanged.
    /// </summary>
    public void Save()
    {
        string json = JsonSerializer.Serialize(Settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
        SettingsChanged?.Invoke();
    }
}
