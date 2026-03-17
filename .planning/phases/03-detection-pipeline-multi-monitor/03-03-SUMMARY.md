---
phase: 03-detection-pipeline-multi-monitor
plan: 03
subsystem: core
tags: [csharp, dotnet, winforms, multi-monitor, corner-detection, wiring, system-events]

# Dependency graph
requires:
  - phase: 03-detection-pipeline-multi-monitor
    plan: 02
    provides: CornerRouter with Rebuild/OnMouseMoved/OnMouseButtonChanged/Dispose; per-(monitor,corner) detector pool

provides:
  - HotSpotApplicationContext wired to CornerRouter (replaces single CornerDetector)
  - DisplaySettingsChanged subscription for live monitor plug/unplug support
  - Phase 3 fully functional end-to-end: multi-monitor detection pipeline complete

affects: [04-settings-ui]

# Tech tracking
tech-stack:
  added:
    - "Microsoft.Win32.SystemEvents.DisplaySettingsChanged — static event for monitor topology change notifications"
  patterns:
    - "Static event unsubscription in DisposeComponents(): SystemEvents.DisplaySettingsChanged -= handler must precede hookmanager and router disposal to prevent memory leak"
    - "Rebuild-on-event: both SettingsChanged and DisplaySettingsChanged call CornerRouter.Rebuild() — single rebuild path handles all topology and config changes"

key-files:
  created: []
  modified:
    - WindowsHotSpot/HotSpotApplicationContext.cs

key-decisions:
  - "SystemEvents.DisplaySettingsChanged unsubscribed first in DisposeComponents() before HookManager and CornerRouter disposal — static event holds a reference to the instance; failing to unsubscribe would prevent GC and leave a stale handler"
  - "SettingsChanged and DisplaySettingsChanged both call _cornerRouter.Rebuild(_configManager.Settings) — same rebuild path for both config changes and topology changes; no special-casing needed"
  - "System.Linq removed: the FirstOrDefault pattern that selected a single active corner from CornerActions is entirely gone; CornerRouter iterates all corners per monitor internally"

patterns-established:
  - "Wire-then-install order: CornerRouter.Rebuild() called before HookManager.Install() — detector pool is populated before any mouse events can arrive"
  - "Static event cleanup pattern: SystemEvents static events unsubscribed in DisposeComponents() before component disposal"

requirements-completed: [MMON-01, MMON-02, MMON-03, MMON-04]

# Metrics
duration: ~10min (including human verification)
completed: 2026-03-18
---

# Phase 3 Plan 03: Detection Pipeline Multi-Monitor — CornerRouter Wiring Summary

**HotSpotApplicationContext rewired to CornerRouter, completing the multi-monitor detection pipeline end-to-end; human-verified on real hardware with migration, Settings round-trip, and corner detection confirmed working**

## Performance

- **Duration:** ~10 min (including human verification checkpoint)
- **Completed:** 2026-03-18
- **Tasks:** 2 (1 auto + 1 human-verify checkpoint)
- **Files modified:** 1

## Accomplishments

- Replaced `_cornerDetector` (single CornerDetector) with `_cornerRouter` (CornerRouter) in HotSpotApplicationContext
- `CornerRouter.Rebuild(_configManager.Settings)` called on startup before `HookManager.Install()` — pool populated before any mouse events arrive
- `HookManager` events wired to `CornerRouter.OnMouseMoved` and `CornerRouter.OnMouseButtonChanged`
- `SettingsChanged` lambda now calls `_cornerRouter.Rebuild()` — removes the old `UpdateSettings` call and the `System.Linq` dependency
- Added `SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged` for live monitor plug/unplug (MMON-02)
- `OnDisplaySettingsChanged` calls `_cornerRouter.Rebuild()` — same rebuild path as settings changes
- `DisposeComponents()` unsubscribes `DisplaySettingsChanged` first (before hookmanager/router disposal) to prevent memory leak
- Human verification confirmed: app starts without crash, migration ran (TopLeft -> TaskView in CornerActions), MonitorConfigs present (empty dict), corner detection fires correctly

## Task Commits

1. **Task 1: Replace CornerDetector with CornerRouter in HotSpotApplicationContext** - `85febb4` (feat)
2. **Task 2: Human-verify Phase 3 detection pipeline end-to-end** - approved by user; no code changes

## Files Created/Modified

- `WindowsHotSpot/HotSpotApplicationContext.cs` - CornerRouter wired in; DisplaySettingsChanged subscribed and unsubscribed; System.Linq removed; Microsoft.Win32 added

## Decisions Made

- Static event `SystemEvents.DisplaySettingsChanged` unsubscribed first in `DisposeComponents()` — static events hold strong references; failing to unsubscribe would prevent GC of the application context.
- Both `SettingsChanged` and `DisplaySettingsChanged` call the same `_cornerRouter.Rebuild(_configManager.Settings)` — no special-casing; Rebuild() handles both config and topology changes uniformly.
- `System.Linq` removed: the `FirstOrDefault` pattern that selected one active corner is replaced by CornerRouter iterating all corners internally.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None. Human verification confirmed settings.json round-tripped correctly (CornerActions with migrated TopLeft entry, MonitorConfigs as empty dict). Phase 3 complete.

## User Setup Required

None — no external service configuration required.

## Next Phase Readiness

- Phase 3 (Detection Pipeline Multi-Monitor) is fully complete — MMON-01 through MMON-04 satisfied
- Phase 4 (Settings UI) can proceed; CornerRouter.Rebuild() accepts AppSettings directly — SettingsForm redesign in Phase 4 will add per-corner UI without further changes to the detection pipeline

## Self-Check: PASSED

- WindowsHotSpot/HotSpotApplicationContext.cs: FOUND
- Commit 85febb4: present in git log

---
*Phase: 03-detection-pipeline-multi-monitor*
*Completed: 2026-03-18*
