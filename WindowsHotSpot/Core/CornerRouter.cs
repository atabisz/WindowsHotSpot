// CornerRouter: owns the per-(monitor, corner) CornerDetector pool.
// Rebuild() tears down existing detectors and creates fresh ones for every active
// (monitor, enabled corner) pair. Called on startup, settings change, and display change.
// OnMouseMoved broadcasts to ALL detectors — each detector's IsInCornerZone gates itself.
// Broadcasting (vs. routing only to the containing screen) is required for inner-edge corners:
// when a corner zone straddles the boundary between two monitors (e.g. TopLeft of a monitor
// that is to the right of another), half the zone sits on the adjacent monitor's side.
// Routing only to the containing screen would cut the effective zone to 20 px instead of 40 px
// and prevent the dwell from starting when the cursor approaches from the adjacent monitor.
// Phase 3: replaces the single CornerDetector in HotSpotApplicationContext.

using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using WindowsHotSpot.Config;

namespace WindowsHotSpot.Core;

internal sealed class CornerRouter : IDisposable
{
    // Pre-cached after each Rebuild(). Each entry: screen + its detectors.
    // Using a record struct keeps the screen-to-detector pairing explicit.
    private readonly List<ScreenDetectors> _pool = new();

    private readonly record struct ScreenDetectors(
        Screen Screen,
        List<CornerDetector> Detectors);

    /// <summary>
    /// Disposes all existing detectors and rebuilds the pool from the current
    /// display topology and settings. Called on startup, SettingsChanged, and
    /// DisplaySettingsChanged.
    /// </summary>
    public void Rebuild(AppSettings settings)
    {
        // Always dispose first — no stale detectors running alongside new ones (Pitfall 6).
        DisposePool();

        // SameOnAllMonitors: pick one config up-front and apply it to every screen.
        // Avoids per-screen DeviceName lookup, which can fail if Windows renumbered
        // monitors (e.g. DISPLAY22→DISPLAY24) between the settings save and this Rebuild.
        MonitorCornerConfig? sharedConfig = settings.SameOnAllMonitors
            ? settings.MonitorConfigs.Values.FirstOrDefault()
            : null;

        foreach (var screen in Screen.AllScreens)
        {
            // Per-monitor override if present; global CornerActions fallback if not (MMON-01, MMON-03).
            MonitorCornerConfig? monitorConfig = sharedConfig; // non-null only when SameOnAllMonitors
            if (monitorConfig == null)
                settings.MonitorConfigs.TryGetValue(screen.DeviceName, out monitorConfig);
            var actions = monitorConfig?.CornerActions ?? settings.CornerActions;

            var detectors = new List<CornerDetector>();
            foreach (var (corner, action) in actions)
            {
                if (action == CornerAction.Disabled) continue;
                CustomShortcut? custom = null;
                if (action == CornerAction.Custom)
                {
                    // monitorConfig is the MonitorCornerConfig for this screen (may be null for fallback path).
                    // TryGetValue silently leaves custom null if this corner has no recorded shortcut.
                    monitorConfig?.CustomShortcuts.TryGetValue(corner, out custom);
                }
                detectors.Add(new CornerDetector(
                    corner,
                    screen.Bounds,
                    settings.ZoneSize,
                    settings.DwellDelayMs,
                    action,
                    custom));
            }

            // Add entry even if detectors list is empty — screen is known but all corners Disabled.
            _pool.Add(new ScreenDetectors(screen, detectors));
        }
    }

    /// <summary>
    /// Broadcasts mouse-moved events to every detector in the pool.
    /// Each detector's IsInCornerZone check (using its own _screenBounds) acts as the gate.
    ///
    /// Broadcasting is necessary for inner-edge corners — zones whose centre is on the
    /// boundary between two monitors. With per-screen routing the effective zone would be
    /// only half-width (the cursor-side half), making the corner unreliable to hit.
    /// With broadcasting, the zone covers both sides of the boundary exactly as intended,
    /// and the dwell correctly cancels whenever the cursor moves far from the corner on
    /// any monitor.
    ///
    /// Screen.AllScreens is NOT called here to keep the hot path free of Win32 P/Invoke (Pitfall 4).
    /// </summary>
    public void OnMouseMoved(Point pt)
    {
        foreach (var entry in _pool)
            foreach (var detector in entry.Detectors)
                detector.OnMouseMoved(pt);
    }

    /// <summary>
    /// Propagates button state to all detectors for drag suppression (CORE-04).
    /// A button-down cancels any in-progress dwell on any monitor.
    /// </summary>
    public void OnMouseButtonChanged(bool isDown)
    {
        foreach (var entry in _pool)
            foreach (var detector in entry.Detectors)
                detector.OnMouseButtonChanged(isDown);
    }

    public void Dispose() => DisposePool();

    private void DisposePool()
    {
        foreach (var entry in _pool)
            foreach (var detector in entry.Detectors)
                detector.Dispose();
        _pool.Clear();
    }
}
