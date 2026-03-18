# Phase 3: Detection Pipeline & Multi-Monitor - Research

**Researched:** 2026-03-18
**Domain:** Multi-monitor dwell detection, CornerRouter architecture, WM_DISPLAYCHANGE handling, per-monitor config in C# .NET 10 WinForms
**Confidence:** HIGH

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| MMON-01 | Each connected monitor has its own independent set of 4 corner configurations | CornerRouter per-monitor dispatch pattern; Screen.DeviceName as config key; per-monitor config storage in AppSettings.MonitorConfigs |
| MMON-02 | Adding or removing a monitor updates the active corner set without restart | SystemEvents.DisplaySettingsChanged subscription; CornerRouter.Rebuild() pattern; Screen.AllScreens cache invalidation confirmed |
| MMON-03 | A new unrecognised monitor defaults to all-corners disabled | Fallback logic: MonitorConfigs lookup miss → new MonitorConfig with all CornerAction.Disabled (already the default enum value) |
| MMON-04 | Config for a disconnected monitor is silently retained for when it reconnects | Persist MonitorConfigs dictionary without pruning disconnected monitors; DeviceName-keyed entries survive reconnect |
</phase_requirements>

---

## Summary

Phase 2 delivered a working AppSettings v2 schema with `CornerAction` enum and a `Dictionary<HotCorner, CornerAction>` per-corner action map, plus ActionDispatcher wired into CornerDetector. However, that model is global — one set of four corners for all monitors. Phase 3 extends the detection pipeline to be per-monitor: the central change is replacing the single `CornerDetector` with a `CornerRouter` that owns one `CornerDetector` per (monitor, enabled corner) pair and rebuilds that pool when monitors are added, removed, or settings change.

The key Win32 integration point is `WM_DISPLAYCHANGE` (0x007E), which Windows sends to all top-level windows when the display resolution changes or a monitor is plugged/unplugged. The .NET equivalent is `SystemEvents.DisplaySettingsChanged`, fired after display settings are committed. The existing `IpcWindow` in `HotSpotApplicationContext` is a `NativeWindow` with its own `WndProc` — it is the right place to catch `WM_DISPLAYCHANGE` and trigger a router rebuild without requiring a visible window. Alternatively, `SystemEvents.DisplaySettingsChanged` is a clean event-based approach that avoids raw WndProc patching.

The `Screen.AllScreens` property uses an internal static cache backed by `EnumDisplayMonitors`. That cache is invalidated automatically on `SystemEvents.DisplaySettingsChanging`. When `CornerRouter.Rebuild()` is called after a display change event, `Screen.AllScreens` will reflect the new monitor topology without any manual cache management. Per-monitor config is stored in `AppSettings` as a `Dictionary<string, MonitorCornerConfig>` keyed by `Screen.DeviceName`. A new `MonitorCornerConfig` class holds a `Dictionary<HotCorner, CornerAction>` and defaults all actions to `Disabled`. Entries for disconnected monitors are retained in the dictionary and the config file, satisfying MMON-04 with no extra work.

**Primary recommendation:** Introduce `CornerRouter` as the new owner of the per-(monitor,corner) `CornerDetector` pool, subscribe to `SystemEvents.DisplaySettingsChanged` for live monitor changes, and extend `AppSettings` with a `Dictionary<string, MonitorCornerConfig>` for per-monitor config keyed by `Screen.DeviceName`.

---

## Standard Stack

### Core (No New NuGet Packages)

| Component | Version | Purpose | Why Standard |
|-----------|---------|---------|--------------|
| `System.Windows.Forms.Screen` | .NET 10 inbox | Enumerate connected monitors; get bounds and DeviceName | Already used in `CornerDetector.IsInAnyCornerZone`; no new dependency |
| `Microsoft.Win32.SystemEvents` | .NET 10 inbox | `DisplaySettingsChanged` event for monitor plug/unplug notification | .NET-idiomatic alternative to raw WM_DISPLAYCHANGE; fires on UI thread via WinForms app |
| `System.Text.Json` | .NET 10 inbox | Serialize `Dictionary<string, MonitorCornerConfig>` | String-keyed dictionaries serialize cleanly; same options already in ConfigManager |
| Win32 `WM_DISPLAYCHANGE` (0x007E) | user32.dll | Raw window message for display change — backup path if SystemEvents insufficient | Delivered to all top-level windows including HWND_MESSAGE windows |

### Per-Monitor Config Shape

The current `AppSettings` model (Phase 2 output):
```csharp
// Phase 2 — global per-corner actions (one set for all monitors)
public Dictionary<HotCorner, CornerAction> CornerActions { get; set; }
```

The Phase 3 extension adds per-monitor overrides:
```csharp
// Phase 3 addition — per-monitor overrides keyed by Screen.DeviceName
// Missing key = use global CornerActions fallback
// Present key = use MonitorCornerConfig.CornerActions for that monitor
public Dictionary<string, MonitorCornerConfig> MonitorConfigs { get; set; } = new();
```

```csharp
internal sealed class MonitorCornerConfig
{
    // Same shape as AppSettings.CornerActions — defaults all to Disabled
    public Dictionary<HotCorner, CornerAction> CornerActions { get; set; }
        = AppSettings.DefaultCornerActions();  // reuse existing helper
}
```

**Why `Dictionary<string, MonitorCornerConfig>` works cleanly with System.Text.Json:** String-keyed dictionaries serialize with the default serializer without a custom converter. The `CornerAction` enum values inside use the existing `[JsonConverter(typeof(JsonStringEnumConverter))]` on the enum declaration — already in AppSettings.cs.

**Why NOT `Dictionary<HotCorner, MonitorCornerConfig>`:** System.Text.Json does not serialize enum-keyed dictionaries with string keys by default without additional converter configuration. The DeviceName string key avoids this.

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `SystemEvents.DisplaySettingsChanged` | `WM_DISPLAYCHANGE` in IpcWindow.WndProc | Both work; SystemEvents is higher-level and requires unsubscription on dispose. WndProc is lower-level but centralizes all window message handling. Either is acceptable; SystemEvents is simpler. |
| `Screen.DeviceName` as monitor key | Screen index (0, 1, 2) | Index changes when monitor order changes in Display Settings; DeviceName is stable while the physical port connection is unchanged |
| One `CornerDetector` per (monitor, corner) pair | One detector checking all corners across all monitors | Per-pair gives independent 3-state machines with no cross-corner state contamination; the existing detector is already proven for one corner |

---

## Architecture Patterns

### Recommended Project Structure (changes from Phase 2)

```
WindowsHotSpot/
├── Config/
│   ├── AppSettings.cs         # +MonitorConfigs dict; +MonitorCornerConfig class
│   └── ConfigManager.cs       # No changes needed (serialization handles new dict)
├── Core/
│   ├── CornerRouter.cs        # NEW — owns pool of CornerDetectors, handles Rebuild()
│   ├── CornerDetector.cs      # MODIFY — constructor takes screen bounds; zone check scoped to screen
│   ├── ActionDispatcher.cs    # No changes (already dispatches CornerAction)
│   └── HookManager.cs         # No changes
└── HotSpotApplicationContext.cs  # MODIFY — CornerRouter replaces single CornerDetector; subscribe to DisplaySettingsChanged
```

### Pattern 1: CornerRouter — Rebuild-on-Change

**What:** `CornerRouter` owns a flat list of `CornerDetector` instances, one per (monitor, enabled corner) pair. On any settings change or display topology change, it disposes all existing detectors and creates a fresh set.

**When to use:** Whenever the set of active detectors can change (corners enabled/disabled, monitors added/removed). Rebuild is O(monitors × 4) — cheap for an infrequent user-driven action.

**Why not diff and update in place:** The enabled set changes; detectors for newly disabled corners must be disposed and detectors for newly enabled corners must be created. Diffing is fragile; a clean rebuild eliminates stale-state bugs.

```csharp
// Source: derived from ARCHITECTURE.md research + existing CornerDetector pattern
internal sealed class CornerRouter : IDisposable
{
    private readonly List<(Screen Screen, CornerDetector Detector)> _detectors = new();

    public void OnMouseMoved(Point pt)
    {
        // Identify which screen contains the point
        // Screen.AllScreens is already-rebuilt after DisplaySettingsChanged
        foreach (var screen in Screen.AllScreens)
        {
            if (!screen.Bounds.Contains(pt)) continue;
            foreach (var (s, detector) in _detectors)
                if (s.DeviceName == screen.DeviceName)
                    detector.OnMouseMoved(pt);
            return;  // point belongs to at most one screen
        }
        // Point not in any screen bounds (can happen at monitor junction edge)
        // No-op — do not dispatch to any detector
    }

    public void OnMouseButtonChanged(bool isDown)
    {
        // Button state applies to ALL detectors (drag suppression)
        foreach (var (_, detector) in _detectors)
            detector.OnMouseButtonChanged(isDown);
    }

    public void Rebuild(AppSettings settings)
    {
        // Dispose existing detectors
        foreach (var (_, d) in _detectors) d.Dispose();
        _detectors.Clear();

        foreach (var screen in Screen.AllScreens)
        {
            // Resolve corner actions: per-monitor override or global fallback
            Dictionary<HotCorner, CornerAction> actions =
                settings.MonitorConfigs.TryGetValue(screen.DeviceName, out var mc)
                    ? mc.CornerActions
                    : settings.CornerActions;

            foreach (var (corner, action) in actions)
            {
                if (action == CornerAction.Disabled) continue;
                _detectors.Add((screen, new CornerDetector(
                    corner, screen.Bounds, settings.ZoneSize,
                    settings.DwellDelayMs, settings.MonitorConfigs,
                    // CornerDetector needs the action directly, not ConfigManager reference
                    action)));
            }
        }
    }

    public void Dispose()
    {
        foreach (var (_, d) in _detectors) d.Dispose();
        _detectors.Clear();
    }
}
```

### Pattern 2: CornerDetector — Screen-Scoped Zone Check

**What:** Modify `CornerDetector` to receive the specific `Screen.Bounds` at construction (instead of iterating `Screen.AllScreens` on every mouse move). The zone check becomes a single bounds comparison against `_screenBounds`.

**Why important:** The current `IsInAnyCornerZone` iterates all screens, which is correct for a single global detector but wrong when there are separate per-screen detectors — a detector for Monitor A's TopRight corner must not fire when the cursor is in Monitor B's TopRight zone.

**Change to CornerDetector constructor:**
```csharp
// Before (Phase 2):
public CornerDetector(HotCorner corner, int zoneSize, int dwellDelay, ConfigManager configManager)

// After (Phase 3):
public CornerDetector(HotCorner corner, Rectangle screenBounds, int zoneSize, int dwellDelay, CornerAction action)
```

**Change to zone check:**
```csharp
// Before: iterates all screens
private bool IsInAnyCornerZone(Point pt) {
    foreach (var screen in Screen.AllScreens) { ... }
}

// After: checks only this detector's screen
private bool IsInCornerZone(Point pt) {
    Point corner = GetCornerPoint(_screenBounds, _activeCorner);
    return Math.Abs(pt.X - corner.X) <= _zoneSize
        && Math.Abs(pt.Y - corner.Y) <= _zoneSize;
}
```

**Change to `OnDwellComplete`:** Remove `ConfigManager` dependency; the action is fixed at construction time via the `CornerAction` parameter.
```csharp
private void OnDwellComplete(object? sender, EventArgs e)
{
    _dwellTimer.Stop();
    ActionDispatcher.Dispatch(_action);  // _action set in constructor; no ConfigManager lookup
    _state = DetectorState.Triggered;
}
```

### Pattern 3: Monitor Change Notification via SystemEvents

**What:** Subscribe to `SystemEvents.DisplaySettingsChanged` in `HotSpotApplicationContext`. On fire, call `_cornerRouter.Rebuild(_configManager.Settings)`.

**Thread safety:** `SystemEvents.DisplaySettingsChanged` fires on the thread that subscribed. In a WinForms STA app with an active message loop, the event fires on the UI thread — safe to call `_cornerRouter.Rebuild()` directly.

**CRITICAL — memory leak guard:** `SystemEvents` events are static; if the handler is an instance method, the instance will never be GC-collected unless the event is unsubscribed. Unsubscribe in `DisposeComponents()`.

```csharp
// In HotSpotApplicationContext constructor:
SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

// Handler:
private void OnDisplaySettingsChanged(object? sender, EventArgs e)
{
    _cornerRouter.Rebuild(_configManager.Settings);
}

// In DisposeComponents():
SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
```

### Pattern 4: Default Config for New Monitor (MMON-03)

**What:** When `CornerRouter.Rebuild()` encounters a monitor whose `DeviceName` is not in `settings.MonitorConfigs`, it falls back to global `settings.CornerActions`. A new unrecognised monitor does NOT get a MonitorConfig entry automatically — it just uses the global config.

**For MMON-03 specifically:** The global `CornerActions` default (Phase 2 baseline) has all four corners set to `CornerAction.Disabled`. A brand new monitor therefore activates no corners — satisfying MMON-03.

**For MMON-04:** MonitorConfig entries for disconnected monitors stay in the `MonitorConfigs` dictionary indefinitely. They are never pruned. On reconnect, `Rebuild()` finds the DeviceName match and restores that monitor's config.

### Anti-Patterns to Avoid

- **Calling `Screen.AllScreens` inside the hook callback chain:** `Screen.AllScreens` calls `EnumDisplayMonitors` (Win32 P/Invoke). This is safe on the UI thread in `OnMouseMoved` (which is reached via the hook firing on the message loop), but the call is unnecessary if the router dispatches by pre-built screen identity. Keep the hot-path: `CornerRouter.OnMouseMoved` checks `screen.Bounds.Contains(pt)` against the already-known screens, not a fresh `Screen.AllScreens` call.
- **Not unsubscribing `SystemEvents.DisplaySettingsChanged`:** Static events cause memory leaks if the subscriber is not unsubscribed. Always unsubscribe in `DisposeComponents()`.
- **Using monitor index as a config key:** Monitor indices change when the user reorders displays or adds a new primary monitor. Only `Screen.DeviceName` is stable for a given physical port connection.
- **Rebuilding CornerDetectors on every `SettingsChanged`:** Rebuild is correct but the rebuild logic must use a fresh `Screen.AllScreens` call each time (not a cached list from startup), since the monitor topology may have changed between settings saves.
- **Passing `ConfigManager` reference into rebuilt `CornerDetector`:** The Phase 2 `CornerDetector` takes a `ConfigManager` reference to look up `CornerActions` at dispatch time. For Phase 3, the detector is created with a fixed `CornerAction` value — this removes the ConfigManager dependency and makes the detector's behavior at dwell-complete time deterministic from construction. If settings change, the router is rebuilt with new detectors.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Monitor change notification | Custom WM_DISPLAYCHANGE WndProc override in IpcWindow | `SystemEvents.DisplaySettingsChanged` | .NET-idiomatic, fires on UI thread in WinForms app, handles unsubscription cleanly |
| Monitor enumeration | Custom P/Invoke for `EnumDisplayMonitors` | `Screen.AllScreens` | Already used and verified in CornerDetector; Screen.AllScreens calls EnumDisplayMonitors internally and handles the cache/invalidation lifecycle |
| Screen cache invalidation | Manual staleness tracking | Rely on Screen.AllScreens internal cache (invalidated on DisplaySettingsChanging) | Confirmed: Screen.AllScreens static cache is nulled on `SystemEvents.DisplaySettingsChanging` before `DisplaySettingsChanged` fires; no manual management needed |
| Corner zone containment per screen | Custom monitor-aware zone math | Screen-scoped `IsInCornerZone` using `_screenBounds` passed at construction | Simple Rectangle-based math already proven in existing CornerDetector |

**Key insight:** The existing WinForms `Screen` class wraps all necessary Win32 monitor APIs. No new P/Invoke declarations are needed for Phase 3. The only new native constant needed is `WM_DISPLAYCHANGE = 0x007E` in NativeMethods.cs if the WndProc path is chosen over SystemEvents.

---

## Common Pitfalls

### Pitfall 1: Cross-Corner State Contamination from Shared Detector

**What goes wrong:** Extending the single `CornerDetector` to track multiple corners inside one state machine (e.g., `List<HotCorner>` with a loop inside `OnMouseMoved`) means the Dwelling state is shared. Mouse entering corner B while corner A's dwell timer is running does not cancel A — A fires the wrong action.

**Why it happens:** `IsInAnyCornerZone` returns `true` without identifying *which* corner matched. Adding more corners to the check without separate state machines blends their identity.

**How to avoid:** One `CornerDetector` instance per active corner. The 3-state machine must remain per-corner. `CornerRouter` owns the collection and dispatches by screen identity.

**Warning signs:** A corner fires when the cursor entered a *different* corner on the same monitor.

### Pitfall 2: Adjacent Monitor Corner Overlap

**What goes wrong:** When monitor A's right edge abuts monitor B's left edge, the cursor at the junction is within `_zoneSize` pixels of both A's TopRight corner and B's TopLeft corner simultaneously. Both detectors fire on the same dwell.

**Why it happens:** With per-screen detectors and a zone-size check of `<= 10`, a point on the exact boundary pixel activates both zones.

**How to avoid:** Scope the zone check to one screen only (`screen.Bounds.Contains(pt)` in the router before dispatching). Since the router routes to only the matching screen's detectors, a point at the junction is assigned to the screen whose `Bounds.Contains(pt)` returns true first. `Screen.Bounds` rectangles for adjacent monitors are contiguous but non-overlapping — `Contains` for a boundary pixel goes to exactly one screen in GDI geometry (`Rectangle.Contains` is inclusive on left/top, exclusive on right/bottom). Document this edge case and test explicitly.

**Warning signs:** Double-triggers on a dual-monitor machine when cursor is at the monitor junction with adjacent corners both enabled.

### Pitfall 3: Memory Leak from Unsubscribed Static Event

**What goes wrong:** `SystemEvents.DisplaySettingsChanged` is a static event on `Microsoft.Win32.SystemEvents`. If the handler is an instance method of `HotSpotApplicationContext` and is never removed, the static event holds a reference to the context, preventing GC collection after the tray app exits (in-process) or leaking across Settings dialog opens.

**Why it happens:** Developers subscribe in the constructor and forget to unsubscribe. The MSDN documentation explicitly warns: "Because this is a static event, you must detach your event handlers when your application is disposed, or memory leaks will result."

**How to avoid:** In `DisposeComponents()`, always call `SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged`.

**Warning signs:** Process memory grows after repeated Settings opens or display changes.

### Pitfall 4: Screen.AllScreens Called Inside Hook Callback

**What goes wrong:** If `CornerRouter.OnMouseMoved` calls `Screen.AllScreens` on every mouse move to identify which screen owns the cursor, it runs `EnumDisplayMonitors` (Win32 P/Invoke) inside the hook callback chain. Normally this is fast, but during a display change event it can block while the screen list is being rebuilt.

**Why it happens:** The router needs to know which screen the point belongs to; the direct approach is `Screen.AllScreens` in the loop. This is technically fine in normal operation but is a latency risk during display topology transitions.

**How to avoid:** Pre-build the active screen list in `Rebuild()` and use it in `OnMouseMoved`. After a display change, `Rebuild()` is called once (capturing the new topology), after which `OnMouseMoved` uses the pre-built list. This also removes the `EnumDisplayMonitors` call from the hot path.

### Pitfall 5: DeviceName Key Mismatch After Driver Re-enumeration

**What goes wrong:** `Screen.DeviceName` (e.g., `"\\.\DISPLAY1"`) is the GDI device string. It is stable for a given physical port connection but can change if the GPU driver re-enumerates displays (e.g., after driver update, display setting changes in GPU control panel) or if the user changes which monitor is primary. Per-monitor config silently stops matching — the monitor falls back to global config instead of its saved override.

**Why it happens:** GDI device names are ordinal-based (`DISPLAY1`, `DISPLAY2`) and can be reassigned by the driver. They are NOT hardware-stable identifiers (unlike EDID or monitor GUIDs via SetupAPI).

**How to avoid:** Accept this as a known limitation — the same approach used by DisplayFusion and comparable tools. Document it in code comments. If a DeviceName key is not found, silently fall back to global config; do not crash or corrupt settings. Provide no automatic recovery (EDID-based lookup requires SetupAPI and is out of scope per REQUIREMENTS.md). The STATE.md blocker entry ("Screen.DeviceName stability as monitor identity key is MEDIUM confidence") acknowledges this; Phase 3 should validate this empirically on the target hardware.

**Warning signs:** Per-monitor config stops applying after GPU driver update; monitor falls back to global config with no user action.

### Pitfall 6: CornerDetector Holds Stale CornerAction After Rebuild

**What goes wrong:** In Phase 2, `CornerDetector` holds a `ConfigManager` reference and calls `_configManager.Settings.CornerActions[_activeCorner]` at dispatch time. If the Phase 3 `Rebuild()` creates new detectors but the old ConfigManager reference is shared, a detector from the previous build generation might still fire using outdated settings if the old detector wasn't disposed.

**Why it happens:** Lifecycle confusion between dispose and rebuild. If `Rebuild()` forgets to dispose old detectors first, they stay active and fire with old config.

**How to avoid:** `CornerRouter.Rebuild()` MUST call `Dispose()` on every existing detector before creating the new set. The action value is baked into the new detector at construction — not looked up dynamically — so there is no shared mutable state between generations.

---

## Code Examples

### CornerRouter.Rebuild() — Full Pattern

```csharp
// Source: derived from existing CornerDetector + Screen.AllScreens patterns; verified against
// official Screen docs (https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.screen)
public void Rebuild(AppSettings settings)
{
    // Always dispose first — no stale detectors running alongside new ones
    foreach (var (_, d) in _detectors) d.Dispose();
    _detectors.Clear();

    foreach (var screen in Screen.AllScreens)
    {
        // Per-monitor override if present; global fallback if not
        var actions = settings.MonitorConfigs.TryGetValue(screen.DeviceName, out var mc)
            ? mc.CornerActions
            : settings.CornerActions;

        foreach (var (corner, action) in actions)
        {
            if (action == CornerAction.Disabled) continue;
            _detectors.Add((
                screen,
                new CornerDetector(corner, screen.Bounds,
                    settings.ZoneSize, settings.DwellDelayMs, action)
            ));
        }
    }
}
```

### HotSpotApplicationContext — Wiring Changes

```csharp
// Constructor additions:
_cornerRouter = new CornerRouter();
_cornerRouter.Rebuild(_configManager.Settings);

_hookManager.MouseMoved += _cornerRouter.OnMouseMoved;
_hookManager.MouseButtonChanged += _cornerRouter.OnMouseButtonChanged;

// Replace the old SettingsChanged lambda:
_configManager.SettingsChanged += () => _cornerRouter.Rebuild(_configManager.Settings);

// Monitor change:
SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

// New handler:
private void OnDisplaySettingsChanged(object? sender, EventArgs e)
    => _cornerRouter.Rebuild(_configManager.Settings);

// In DisposeComponents() — MUST include:
SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
_hookManager.MouseMoved -= _cornerRouter.OnMouseMoved;
_hookManager.MouseButtonChanged -= _cornerRouter.OnMouseButtonChanged;
_cornerRouter.Dispose();
```

### AppSettings — Phase 3 Additions

```csharp
// Add to AppSettings.cs (no changes to existing CornerActions or DefaultCornerActions):
public Dictionary<string, MonitorCornerConfig> MonitorConfigs { get; set; } = new();

internal sealed class MonitorCornerConfig
{
    // Reuses DefaultCornerActions() helper — all corners Disabled by default (MMON-03)
    public Dictionary<HotCorner, CornerAction> CornerActions { get; set; }
        = AppSettings.DefaultCornerActions();
}
```

### NativeMethods.cs — Optional WM_DISPLAYCHANGE Constant

```csharp
// Add if WndProc path chosen over SystemEvents (either approach is valid):
// Source: https://learn.microsoft.com/en-us/windows/win32/gdi/wm-displaychange
public const int WM_DISPLAYCHANGE = 0x007E;
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Single `CornerDetector` for one global corner | `CornerRouter` owning N detectors (one per active corner per monitor) | Phase 3 | Enables per-monitor independent dwell detection |
| `CornerDetector` iterates `Screen.AllScreens` in zone check | Zone check scoped to `_screenBounds` (passed at construction) | Phase 3 | Eliminates cross-monitor zone overlap; removes Screen.AllScreens from hot path |
| `CornerDetector` holds `ConfigManager` for live config lookup | `CornerDetector` holds fixed `CornerAction` baked at construction | Phase 3 | Simpler lifetime; no shared mutable state between detector generations |
| Global `CornerActions` only | `MonitorConfigs` dict with global fallback | Phase 3 | Per-monitor independent configuration (MMON-01) |

---

## Open Questions

1. **`Screen.AllScreens` pre-caching in CornerRouter vs on-demand**
   - What we know: `Screen.AllScreens` uses a static cache invalidated on `DisplaySettingsChanging` (before `DisplaySettingsChanged`). Calling it in `OnMouseMoved` is safe but does P/Invoke on cache miss (during display changes).
   - What's unclear: Whether calling `Screen.AllScreens` in the hot-path of `OnMouseMoved` causes measurable latency during a display change event when the cache is being rebuilt.
   - Recommendation: Pre-cache the screen list in `CornerRouter._screens` at `Rebuild()` time. Use `_screens` in `OnMouseMoved` instead of `Screen.AllScreens`. This eliminates the question entirely.

2. **SystemEvents.DisplaySettingsChanged vs WM_DISPLAYCHANGE**
   - What we know: Both notifications are reliable. `SystemEvents` is .NET-idiomatic. `WM_DISPLAYCHANGE` requires a WndProc override. The existing `IpcWindow` already has a WndProc.
   - What's unclear: Which fires first or more reliably in edge cases (display disable without resolution change, GPU driver reload).
   - Recommendation: Use `SystemEvents.DisplaySettingsChanged`. It is the documented .NET approach. The existing code already uses `SystemEvents` for startup manager. If edge cases arise, add `WM_DISPLAYCHANGE` in `IpcWindow.WndProc` as a belt-and-suspenders secondary trigger.

3. **MonitorCornerConfig in settings.json — dict vs list serialization**
   - What we know: `Dictionary<string, MonitorCornerConfig>` with string keys serializes cleanly with System.Text.Json. The inner `Dictionary<HotCorner, CornerAction>` uses enum-string keys (same pattern as existing `CornerActions`). Phase 2 established that `Dictionary<HotCorner, CornerAction>` serializes correctly with the existing `JsonOptions`.
   - What's unclear: Whether the nested dictionary roundtrip is verified in the current ConfigManager test path.
   - Recommendation: Verify JSON roundtrip of the full nested structure (`MonitorConfigs` → `MonitorCornerConfig` → `CornerActions`) as the first task in Phase 3 implementation, before wiring any detection logic. A broken serialization will silently drop per-monitor config on restart.

---

## Sources

### Primary (HIGH confidence)

- `C:/src/WindowsHotSpot/WindowsHotSpot/Core/CornerDetector.cs` — direct source inspection; Phase 2 state
- `C:/src/WindowsHotSpot/WindowsHotSpot/Config/AppSettings.cs` — current schema (CornerAction enum, CornerActions dict)
- `C:/src/WindowsHotSpot/WindowsHotSpot/HotSpotApplicationContext.cs` — current wiring; IpcWindow WndProc pattern
- `C:/src/WindowsHotSpot/.planning/research/ARCHITECTURE.md` — CornerRouter design; per-monitor config model; anti-patterns
- `C:/src/WindowsHotSpot/.planning/research/PITFALLS.md` — cross-corner state contamination; adjacent monitor overlap; schema break pitfalls
- `C:/src/WindowsHotSpot/.planning/research/STACK.md` — System.Text.Json enum-keyed dictionary constraints; Screen.DeviceName rationale
- `https://learn.microsoft.com/en-us/windows/win32/gdi/wm-displaychange` — WM_DISPLAYCHANGE message spec; sent to all windows on display resolution change
- `https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.screen.allscreens` — AllScreens property; uses internal static cache
- `https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.systemevents.displaysettingschanged` — DisplaySettingsChanged event; static event memory leak warning; fires in WinForms STA app on UI thread

### Secondary (MEDIUM confidence)

- `Screen.AllScreens` cache invalidation behavior: confirmed via Screen.cs source analysis (nulled on DisplaySettingsChanging), not formally documented in public API docs
- `Screen.DeviceName` stability: standard practice in comparable apps (DisplayFusion, BetterTouchTool); GDI device string can change on driver re-enumeration — accepted limitation

### Tertiary (LOW confidence)

- `WM_DISPLAYCHANGE` vs `SystemEvents.DisplaySettingsChanged` reliability in edge cases (display disable without resolution change): based on general Win32 knowledge, not verified empirically

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — all APIs are existing inbox (.NET 10 WinForms) or already used in codebase; no new packages
- Architecture: HIGH — CornerRouter design derived directly from existing CornerDetector + Phase 2 source; Screen.AllScreens behavior confirmed from source
- Pitfalls: HIGH — most derived from existing codebase analysis and established Win32 contracts; DeviceName stability is MEDIUM

**Research date:** 2026-03-18
**Valid until:** 2026-06-18 (stable APIs — .NET 10 WinForms, Win32 GDI; 90 days)
