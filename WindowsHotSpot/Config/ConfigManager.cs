// ConfigManager: loads/saves AppSettings as JSON, fires SettingsChanged on save.
// MigrateV1(): promotes v1 "Corner" field to CornerActions on first load from a v1 file.
// SaveFile(): writes JSON silently (no event); used by migration to avoid premature SettingsChanged.
// Fill-missing-keys: after load, ensures all four HotCorner keys are present in CornerActions.

using System.IO;
using System.Linq;
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
    /// Loads settings from settings.json. Migrates v1 "Corner" field if present.
    /// Falls back to defaults on missing or corrupt file.
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
                {
                    Settings = loaded;
                    MigrateV1();
                    // Ensure all four corners are present after migration (partial/corrupt JSON guard)
                    foreach (HotCorner c in Enum.GetValues<HotCorner>())
                        Settings.CornerActions.TryAdd(c, CornerAction.Disabled);
                }
            }
            catch
            {
                // Corrupt file: silently fall back to defaults
                Settings = new AppSettings();
            }
        }

        // Fix stale registry path after exe was moved
        if (Settings.StartWithWindows)
            StartupManager.RefreshPath();
    }

    /// <summary>
    /// Serializes current settings to settings.json and fires SettingsChanged.
    /// </summary>
    public void Save()
    {
        SaveFile();
        SettingsChanged?.Invoke();
    }

    // Writes JSON to disk without firing SettingsChanged.
    // Used by MigrateV1() to persist the migrated file before any listeners are attached.
    private void SaveFile()
    {
        string json = JsonSerializer.Serialize(Settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }

    // Promotes v1 "Corner" field to CornerActions[corner] = TaskView.
    // No-op if LegacyCorner is null (already v2) or if any corner is non-Disabled (user set v2 data).
    private void MigrateV1()
    {
        bool allDisabled = Settings.CornerActions.Values.All(a => a == CornerAction.Disabled);
        if (Settings.LegacyCorner is HotCorner legacy && allDisabled)
        {
            Settings.CornerActions[legacy] = CornerAction.TaskView;
            Settings.LegacyCorner = null;
            SaveFile();
        }
    }
}
