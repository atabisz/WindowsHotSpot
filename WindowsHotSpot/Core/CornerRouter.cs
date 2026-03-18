// CornerRouter: owns the per-(monitor, corner) CornerDetector pool.
// Rebuild() tears down existing detectors and creates fresh ones for every active
// (monitor, enabled corner) pair. Called on startup, settings change, and display change.
// OnMouseMoved routes to detectors for the screen that contains the cursor point.
// Screen list is pre-cached in Rebuild() — Screen.AllScreens is NOT called in OnMouseMoved.
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

        foreach (var screen in Screen.AllScreens)
        {
            // Per-monitor override if present; global CornerActions fallback if not (MMON-01, MMON-03).
            settings.MonitorConfigs.TryGetValue(screen.DeviceName, out var monitorConfig);
            // SameOnAllMonitors: newly connected monitors not yet in MonitorConfigs use any available config
            if (monitorConfig == null && settings.SameOnAllMonitors && settings.MonitorConfigs.Count > 0)
                monitorConfig = settings.MonitorConfigs.Values.First();
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
    /// Routes mouse-moved events to detectors for the screen that contains pt.
    /// Uses the pre-cached screen list from the last Rebuild() call — Screen.AllScreens
    /// is NOT called here to keep the hot path free of Win32 P/Invoke (Pitfall 4).
    /// A point at a monitor boundary goes to the first matching screen (GDI Bounds are
    /// contiguous, non-overlapping — Contains is inclusive left/top, exclusive right/bottom).
    /// </summary>
    public void OnMouseMoved(Point pt)
    {
        foreach (var entry in _pool)
        {
            if (!entry.Screen.Bounds.Contains(pt)) continue;
            foreach (var detector in entry.Detectors)
                detector.OnMouseMoved(pt);
            return; // point belongs to at most one screen
        }
        // Point outside all known screen bounds (can occur at a junction edge during
        // display topology change, before Rebuild() fires). Safe to ignore.
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
