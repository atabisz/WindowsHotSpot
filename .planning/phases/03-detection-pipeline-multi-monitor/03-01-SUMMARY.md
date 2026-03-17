---
phase: 03-detection-pipeline-multi-monitor
plan: 01
subsystem: config
tags: [csharp, dotnet, winforms, multi-monitor, corner-detection, appsettings]

# Dependency graph
requires:
  - phase: 02-config-foundation
    provides: AppSettings POCO with CornerActions dictionary and CornerAction enum; ActionDispatcher; HotCorner enum

provides:
  - MonitorCornerConfig class with per-monitor CornerActions dictionary defaulting to Disabled
  - MonitorConfigs property on AppSettings keyed by Screen.DeviceName
  - CornerDetector refactored to screen-scoped constructor (Rectangle screenBounds + CornerAction); no ConfigManager dependency

affects: [03-02, 03-03, 04-settings-ui]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Screen-scoped detector: CornerDetector takes fixed Rectangle+CornerAction at construction; rebuilt by CornerRouter on changes rather than mutated in-place"
    - "Per-monitor config dictionary keyed by Screen.DeviceName (GDI device string); missing key falls back to global CornerActions"

key-files:
  created: []
  modified:
    - WindowsHotSpot/Config/AppSettings.cs
    - WindowsHotSpot/Core/CornerDetector.cs

key-decisions:
  - "CornerDetector fields changed to readonly — construction-time binding makes mutation impossible and makes the immutable contract explicit"
  - "IsInCornerZone checks only _screenBounds (not Screen.AllScreens) — each detector owns exactly one screen; CornerRouter will own the fan-out"
  - "MonitorCornerConfig placed after AppSettings class in same file — keeps related config types co-located without a new file"

patterns-established:
  - "Immutable detector pattern: CornerDetector is rebuilt rather than mutated; UpdateSettings() gone"
  - "Disabled-by-default per-monitor config: DefaultCornerActions() reused in MonitorCornerConfig so new/unknown monitors fire no actions"

requirements-completed: [MMON-01, MMON-03, MMON-04]

# Metrics
duration: 8min
completed: 2026-03-18
---

# Phase 3 Plan 01: Detection Pipeline Multi-Monitor — Data Model and Detector Foundations Summary

**MonitorCornerConfig per-monitor data model added to AppSettings and CornerDetector refactored to screen-scoped immutable constructor with fixed Rectangle and CornerAction, eliminating ConfigManager dependency**

## Performance

- **Duration:** 8 min
- **Started:** 2026-03-17T23:07:48Z
- **Completed:** 2026-03-17T23:15:00Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments

- Added MonitorCornerConfig sealed class with CornerActions dictionary defaulting to Disabled via DefaultCornerActions() — satisfies MMON-03 (new unrecognised monitors fire no actions)
- Added MonitorConfigs property (Dictionary<string, MonitorCornerConfig>) to AppSettings keyed by Screen.DeviceName — disconnected monitor entries retained indefinitely (MMON-04)
- Refactored CornerDetector constructor from (HotCorner, int, int, ConfigManager) to (HotCorner, Rectangle, int, int, CornerAction); fields are now readonly
- Removed UpdateSettings() method — CornerRouter will rebuild detector pool on settings changes
- Renamed IsInAnyCornerZone to IsInCornerZone; checks only _screenBounds instead of iterating Screen.AllScreens

## Task Commits

1. **Task 1: Extend AppSettings with MonitorCornerConfig and MonitorConfigs** - `c1f37cf` (feat)
2. **Task 2: Refactor CornerDetector to screen-scoped, ConfigManager-free** - `d52b7a1` (refactor)

**Plan metadata:** _(final docs commit hash — see below)_

## Files Created/Modified

- `WindowsHotSpot/Config/AppSettings.cs` - Added MonitorCornerConfig class and MonitorConfigs dictionary property
- `WindowsHotSpot/Core/CornerDetector.cs` - Refactored to screen-scoped immutable constructor; removed ConfigManager and UpdateSettings

## Decisions Made

- Made CornerDetector fields readonly (not in plan spec) — the plan's intent was immutability; making fields readonly enforces this at compile time with no behavioral cost.
- MonitorCornerConfig placed after the AppSettings class in the same file — avoids creating a new file for a tightly coupled type.

## Deviations from Plan

None - plan executed exactly as written. The readonly field change is consistent with the plan's stated intent ("fixed action from construction") and does not alter behavior.

## Issues Encountered

None. Build errors exist only in HotSpotApplicationContext.cs (2 errors on old CornerDetector constructor call and UpdateSettings call) — this is the expected state documented in the plan. Plan 03 will resolve these errors when CornerRouter is wired.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- AppSettings.MonitorConfigs dictionary is ready to be populated by SettingsForm (Phase 4)
- CornerDetector is ready to be instantiated by CornerRouter (Plan 02/03 of this phase)
- HotSpotApplicationContext.cs has two build errors that Plan 03 will fix when CornerRouter replaces the old single-detector wiring

## Self-Check: PASSED

- AppSettings.cs: FOUND
- CornerDetector.cs: FOUND
- 03-01-SUMMARY.md: FOUND
- Commit c1f37cf: FOUND
- Commit d52b7a1: FOUND

---
*Phase: 03-detection-pipeline-multi-monitor*
*Completed: 2026-03-18*
