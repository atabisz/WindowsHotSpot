---
phase: 03-detection-pipeline-multi-monitor
plan: 02
subsystem: core
tags: [csharp, dotnet, winforms, multi-monitor, corner-detection, corner-router]

# Dependency graph
requires:
  - phase: 03-detection-pipeline-multi-monitor
    plan: 01
    provides: CornerDetector with screen-scoped constructor (Rectangle+CornerAction); MonitorCornerConfig; AppSettings.MonitorConfigs dictionary

provides:
  - CornerRouter class owning the per-(monitor, corner) CornerDetector pool
  - Rebuild(AppSettings) tears down and recreates detectors from current display topology and settings
  - OnMouseMoved(Point) routes to matching screen's detectors via pre-cached _pool
  - OnMouseButtonChanged(bool) propagates drag-suppression state to all detectors
  - IDisposable implementation disposing all detectors cleanly

affects: [03-03, 04-settings-ui]

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Pool-with-routing pattern: CornerRouter owns List<ScreenDetectors> where each entry pairs a Screen with its CornerDetector list; Rebuild() is the sole creation point"
    - "Pre-cached screen list: Screen.AllScreens captured in Rebuild(), not called in hot-path OnMouseMoved"
    - "record struct for pairing: ScreenDetectors record struct keeps screen+detector-list paired with named fields, avoiding unnamed tuple items"

key-files:
  created:
    - WindowsHotSpot/Core/CornerRouter.cs
  modified: []

key-decisions:
  - "Add entry to _pool even for screens with all-Disabled corners — screen is known to router; OnMouseMoved matches it and hits empty inner foreach (no-op) rather than falling through to the outside-all-screens case"
  - "DisposePool() extracted as private helper shared by Dispose() and start of Rebuild() — ensures identical teardown path in both cases"
  - "record struct ScreenDetectors used instead of tuple — named fields (Screen, Detectors) make foreach iteration readable without unnamed Item1/Item2"

patterns-established:
  - "Rebuild-not-mutate: CornerRouter always disposes all detectors before creating new ones; no UpdateSettings/partial-rebuild path"
  - "Hot-path isolation: Screen.AllScreens (Win32 P/Invoke) called only in Rebuild(); OnMouseMoved uses pre-cached _pool"

requirements-completed: [MMON-01, MMON-02, MMON-03, MMON-04]

# Metrics
duration: 2min
completed: 2026-03-18
---

# Phase 3 Plan 02: Detection Pipeline Multi-Monitor — CornerRouter Summary

**CornerRouter created as the central pool manager owning one CornerDetector per active (monitor, corner) pair, with pre-cached screen routing and full IDisposable cleanup**

## Performance

- **Duration:** 2 min
- **Started:** 2026-03-18T18:12:19Z
- **Completed:** 2026-03-18T18:14:00Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments

- Created CornerRouter.cs with Rebuild(), OnMouseMoved(), OnMouseButtonChanged(), and Dispose()
- Rebuild() disposes the existing pool first (no stale detectors), then iterates Screen.AllScreens, resolving per-monitor config via MonitorConfigs[DeviceName] with global CornerActions as fallback
- OnMouseMoved routes to the correct screen's detectors using _pool (Screen.AllScreens not called in hot path)
- OnMouseButtonChanged propagates button-down state to all detectors on all screens for drag suppression

## Task Commits

1. **Task 1: Create CornerRouter with Rebuild, routing, and disposal** - `df08fe7` (feat)

**Plan metadata:** _(final docs commit hash — see below)_

## Files Created/Modified

- `WindowsHotSpot/Core/CornerRouter.cs` - CornerRouter with per-(monitor,corner) detector pool, screen-routing, and IDisposable

## Decisions Made

- Added _pool entry even for screens where all corners are Disabled — keeps screen known to the router; OnMouseMoved hits empty inner foreach (no-op) rather than falling through to the "outside all screens" path.
- Used `record struct ScreenDetectors` instead of a tuple — named fields keep the pairing readable without relying on Item1/Item2.
- DisposePool() extracted as a shared private helper used by both Dispose() and the start of Rebuild() to guarantee the same teardown path in both cases.

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None. Build errors exist only in HotSpotApplicationContext.cs (2 errors: old CornerDetector constructor call and UpdateSettings call) — this is the expected state documented in the plan. Plan 03 will wire CornerRouter and eliminate those errors.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- CornerRouter is ready for Plan 03 to wire into HotSpotApplicationContext replacing the old single-detector field
- HotSpotApplicationContext.cs still has 2 build errors (expected); Plan 03 resolves them
- CornerRouter.Rebuild() accepts AppSettings directly — no ConfigManager coupling

## Self-Check: PASSED

- WindowsHotSpot/Core/CornerRouter.cs: FOUND
- Commit df08fe7: present in git log

---
*Phase: 03-detection-pipeline-multi-monitor*
*Completed: 2026-03-18*
