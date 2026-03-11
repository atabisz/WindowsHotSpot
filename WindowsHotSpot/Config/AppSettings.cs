// AppSettings: settings POCO and HotCorner enum (moved from Core/CornerDetector.cs in Phase 2).
// HotCorner serializes as string ("TopLeft") via JsonStringEnumConverter.

using System.Text.Json.Serialization;

namespace WindowsHotSpot.Config;

[JsonConverter(typeof(JsonStringEnumConverter))]
internal enum HotCorner { TopLeft, TopRight, BottomLeft, BottomRight }

internal sealed class AppSettings
{
    public HotCorner Corner { get; set; } = HotCorner.TopLeft;
    public int ZoneSize { get; set; } = 10;
    public int DwellDelayMs { get; set; } = 300;
    public bool StartWithWindows { get; set; } = false;
}
