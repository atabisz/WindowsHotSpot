---
phase: 02-config-foundation
plan: 02
subsystem: config
tags: [csharp, json, settings, migration, v1-compat]

# Dependency graph
requires:
  - 02-01 (AppSettings.LegacyCorner and CornerActions dictionary)
provides:
  - ConfigManager.MigrateV1() method that promotes v1 "Corner" to CornerActions
  - ConfigManager.SaveFile() private helper for silent disk writes
  - Fill-missing-keys logic ensuring all four HotCorner keys always present after Load()
affects:
  - 02-03 (ActionDispatcher reads CornerActions which is now fully populated)
  - 02-04 (HotSpotApplicationContext and SettingsForm consume fully-populated CornerActions)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "SaveFile() private helper pattern: disk write without event notification"
    - "Guard condition pattern: check allDisabled before running migration to avoid re-migrating user-chosen all-Disabled v2 configs"
    - "TryAdd fill pattern: ensure dictionary completeness after deserialization"

key-files:
  created: []
  modified:
    - WindowsHotSpot/Config/ConfigManager.cs

key-decisions:
  - "MigrateV1 guard uses allDisabled check so a v2 user who chose all-Disabled is not accidentally re-migrated if LegacyCorner were somehow present"
  - "Fill-missing-keys runs after MigrateV1 so migrated result is also completed with any absent corners"
  - "SaveFile() called from Save() to remove duplication — Save() is now a thin wrapper that adds the event"

requirements-completed: [CONF-01]

# Metrics
duration: 2min
completed: 2026-03-17
---

# Phase 2 Plan 02: ConfigManager v1 Migration Summary

**ConfigManager extended with MigrateV1(), SaveFile() private helper, and fill-missing-keys logic to silently upgrade v1 settings.json on first load without firing SettingsChanged**

## Performance

- **Duration:** ~2 min
- **Started:** 2026-03-17T05:48:32Z
- **Completed:** 2026-03-17T05:50:00Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments

- Added `SaveFile()` private helper that writes JSON to disk without firing `SettingsChanged` — used exclusively for migration and refactored into `Save()` to remove duplication
- Added `MigrateV1()` private method: detects v1 `LegacyCorner` field, promotes it to `CornerActions[legacy] = TaskView`, clears the field, and persists via `SaveFile()` with no event
- Added fill-missing-keys loop in `Load()` using `TryAdd()` — ensures all four `HotCorner` values are always present in `CornerActions` after deserialization regardless of partial/corrupt JSON
- Correct call order in `Load()`: assign `Settings`, call `MigrateV1()`, then fill missing keys

## Task Commits

Each task was committed atomically:

1. **Task 1: Add MigrateV1, SaveFile, and fill-missing-keys to ConfigManager** - `b4cbaf1` (feat)

**Plan metadata:** _(docs commit pending)_

## Files Created/Modified

- `WindowsHotSpot/Config/ConfigManager.cs` - migration logic: MigrateV1(), SaveFile(), fill-missing-keys, Load() call order

## Decisions Made

- `Save()` refactored to call `SaveFile()` internally — eliminates JSON serialization duplication and makes it clear both paths write the same way
- Migration guard checks `allDisabled` before promoting `LegacyCorner` — protects v2 users who genuinely want all corners disabled from being re-migrated
- Fill-missing-keys runs after `MigrateV1()` so that the migrated corner entry is in place before the loop completes any absent keys

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

- Build errors in `HotSpotApplicationContext.cs` and `SettingsForm.cs` are expected (pre-existing from Plan 01) — those files still reference the removed `Corner` property and are updated in Plan 04.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- ConfigManager now fully handles v1-to-v2 migration on startup
- Plan 03 (ActionDispatcher) reads `CornerActions` which is guaranteed to be fully populated
- Plan 04 (HotSpotApplicationContext / SettingsForm) will resolve the remaining build errors

## Self-Check: PASSED

- FOUND: WindowsHotSpot/Config/ConfigManager.cs
- FOUND: .planning/phases/02-config-foundation/02-02-SUMMARY.md
- FOUND: commit b4cbaf1 (feat(02-02): add MigrateV1, SaveFile, and fill-missing-keys to ConfigManager)

---
*Phase: 02-config-foundation*
*Completed: 2026-03-17*
