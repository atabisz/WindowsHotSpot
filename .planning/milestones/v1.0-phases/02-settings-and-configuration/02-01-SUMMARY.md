---
phase: 02-settings-and-configuration
plan: 01
subsystem: config
tags: [system-text-json, winforms, registry, settings]

requires:
  - phase: 01-core-detection-and-system-tray
    provides: CornerDetector and HotSpotApplicationContext that this phase refactors

provides:
  - AppSettings POCO with HotCorner enum (moved from Core) and 4 configurable properties
  - ConfigManager with JSON load/save and SettingsChanged event
  - StartupManager with HKCU Run key read/write
  - CornerDetector refactored to accept runtime-mutable settings via UpdateSettings
  - HotSpotApplicationContext wired to load settings and propagate changes live

affects: [02-02-settings-dialog-ui]

tech-stack:
  added: [System.Text.Json (inbox), Microsoft.Win32.Registry (inbox)]
  patterns: [settings-POCO-with-event, live-settings-propagation-via-event]

key-files:
  created:
    - WindowsHotSpot/Config/AppSettings.cs
    - WindowsHotSpot/Config/ConfigManager.cs
    - WindowsHotSpot/Config/StartupManager.cs
  modified:
    - WindowsHotSpot/Core/CornerDetector.cs
    - WindowsHotSpot/HotSpotApplicationContext.cs

key-decisions:
  - "HotCorner enum moved to Config namespace so ConfigManager can reference it without circular dependency"
  - "ConfigManager.Load() falls back to new AppSettings() on any exception (corrupt file resilience)"
  - "StartupManager uses Environment.ProcessPath not Assembly.Location (correct for single-file publish)"
  - "UpdateSettings() resets _state to Idle to prevent stale Dwelling trigger if corner changes mid-dwell"

requirements-completed: [CONF-01, CONF-02, CONF-03, CONF-04, CONF-05, SETT-05]

duration: 1min
completed: 2026-03-11
---

# Phase 02 Plan 01: Config Infrastructure Summary

**JSON settings system (ConfigManager + AppSettings) with HKCU Run registry management (StartupManager), CornerDetector refactored to accept mutable settings with live UpdateSettings propagation**

## Performance

- **Duration:** 1 min
- **Started:** 2026-03-11T01:08:05Z
- **Completed:** 2026-03-11T01:09:59Z
- **Tasks:** 2
- **Files modified:** 5

## Accomplishments

- Created Config/ layer with AppSettings POCO, ConfigManager (JSON persistence + SettingsChanged event), and StartupManager (HKCU registry key)
- Refactored CornerDetector to accept (corner, zoneSize, dwellDelay) constructor and expose UpdateSettings() for live reconfiguration
- Wired HotSpotApplicationContext to load settings on startup, pass to CornerDetector, and subscribe SettingsChanged for zero-restart propagation

## Task Commits

1. **Task 1: Create Config layer (AppSettings, ConfigManager, StartupManager)** - `234c7e4` (feat)
2. **Task 2: Refactor CornerDetector and wire ConfigManager into ApplicationContext** - `88609f5` (feat)

## Files Created/Modified

- `WindowsHotSpot/Config/AppSettings.cs` - Settings POCO with HotCorner enum and 4 properties with defaults
- `WindowsHotSpot/Config/ConfigManager.cs` - JSON load/save, SettingsPath in AppContext.BaseDirectory, SettingsChanged event
- `WindowsHotSpot/Config/StartupManager.cs` - HKCU Run key read/write/refresh using Environment.ProcessPath
- `WindowsHotSpot/Core/CornerDetector.cs` - Removed HotCorner enum, constructor now takes 3 params, added UpdateSettings()
- `WindowsHotSpot/HotSpotApplicationContext.cs` - Creates ConfigManager, loads settings, wires SettingsChanged

## Decisions Made

- HotCorner enum moved to Config namespace to avoid circular dependency (Config -> Core would be wrong direction)
- Environment.ProcessPath used in StartupManager instead of Assembly.Location (returns empty string in single-file publish)
- UpdateSettings() stops the timer and resets state to Idle to prevent a stale Dwelling->Triggered transition if the active corner changes while the timer is already running

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- Config infrastructure complete; Plan 02-02 can build SettingsForm on top of AppSettings, ConfigManager, and StartupManager
- All Plan 02-01 must-haves satisfied: Load(), Save(), SettingsChanged, UpdateSettings, IsEnabled/SetEnabled, JSON fallback
- Build: 0 errors, 0 warnings

---
*Phase: 02-settings-and-configuration*
*Completed: 2026-03-11*
