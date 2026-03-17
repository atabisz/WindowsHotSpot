// AppSettings: settings POCO, HotCorner enum, and CornerAction enum.
// HotCorner and CornerAction both serialize as strings via JsonStringEnumConverter.
// LegacyCorner captures the v1 "Corner" field for migration in ConfigManager.MigrateV1().
// Phase 3: MonitorCornerConfig class and MonitorConfigs dictionary added for per-monitor support.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace WindowsHotSpot.Config;

[JsonConverter(typeof(JsonStringEnumConverter))]
internal enum HotCorner { TopLeft, TopRight, BottomLeft, BottomRight }

[JsonConverter(typeof(JsonStringEnumConverter))]
internal enum CornerAction { Disabled, TaskView, ShowDesktop, ActionCenter }

internal sealed class AppSettings
{
    public Dictionary<HotCorner, CornerAction> CornerActions { get; set; } = DefaultCornerActions();

    // v1 migration: captures "Corner" field from old settings.json.
    // Written as nothing when null (WhenWritingNull) so migrated files don't re-grow the field.
    [JsonPropertyName("Corner")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public HotCorner? LegacyCorner { get; set; }

    public int ZoneSize { get; set; } = 10;
    public int DwellDelayMs { get; set; } = 150;
    public bool StartWithWindows { get; set; } = false;

    // Per-monitor corner config keyed by Screen.DeviceName.
    // Missing key = use global CornerActions fallback.
    // Entries for disconnected monitors are retained indefinitely (MMON-04).
    public Dictionary<string, MonitorCornerConfig> MonitorConfigs { get; set; } = new();

    public static Dictionary<HotCorner, CornerAction> DefaultCornerActions() =>
        new()
        {
            [HotCorner.TopLeft]     = CornerAction.Disabled,
            [HotCorner.TopRight]    = CornerAction.Disabled,
            [HotCorner.BottomLeft]  = CornerAction.Disabled,
            [HotCorner.BottomRight] = CornerAction.Disabled,
        };
}

internal sealed class MonitorCornerConfig
{
    // All corners default to Disabled — new unrecognised monitors fire no actions (MMON-03).
    // Reuses DefaultCornerActions() helper to stay consistent with AppSettings defaults.
    public Dictionary<HotCorner, CornerAction> CornerActions { get; set; }
        = AppSettings.DefaultCornerActions();
}
