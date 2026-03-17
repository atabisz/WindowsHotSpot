// AppSettings: settings POCO, HotCorner enum, and CornerAction enum.
// HotCorner and CornerAction both serialize as strings via JsonStringEnumConverter.
// LegacyCorner captures the v1 "Corner" field for migration in ConfigManager.MigrateV1().

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

    public static Dictionary<HotCorner, CornerAction> DefaultCornerActions() =>
        new()
        {
            [HotCorner.TopLeft]     = CornerAction.Disabled,
            [HotCorner.TopRight]    = CornerAction.Disabled,
            [HotCorner.BottomLeft]  = CornerAction.Disabled,
            [HotCorner.BottomRight] = CornerAction.Disabled,
        };
}
