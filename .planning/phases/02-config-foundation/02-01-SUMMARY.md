---
phase: 02-config-foundation
plan: 01
subsystem: config
tags: [csharp, json, settings, enum, dictionary]

# Dependency graph
requires: []
provides:
  - CornerAction enum with four values (Disabled, TaskView, ShowDesktop, ActionCenter)
  - AppSettings.CornerActions Dictionary<HotCorner, CornerAction> replacing old Corner property
  - AppSettings.LegacyCorner with [JsonPropertyName("Corner")] for v1 migration
  - DefaultCornerActions() factory method returning all-Disabled dictionary
affects:
  - 02-02 (ConfigManager migration reads LegacyCorner)
  - 02-03 (ActionDispatcher uses CornerAction enum as dispatch parameter)
  - 02-04 (SettingsForm and HotSpotApplicationContext consume CornerActions dictionary)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "v1 migration hook via [JsonPropertyName] attribute on nullable property"
    - "Dictionary-keyed enum for per-corner configuration"
    - "Static factory method DefaultCornerActions() for safe default initialization"

key-files:
  created: []
  modified:
    - WindowsHotSpot/Config/AppSettings.cs

key-decisions:
  - "CornerAction serializes as string via JsonStringEnumConverter matching HotCorner convention"
  - "LegacyCorner uses [JsonIgnore(Condition = WhenWritingNull)] so migrated files don't re-grow the old field"
  - "DefaultCornerActions() is public static so ConfigManager can call it during migration"

patterns-established:
  - "Nullable property with [JsonPropertyName] as a zero-overhead v1 migration hook"

requirements-completed: [CRNA-01, CRNA-02, CRNA-03, CRNA-04, CRNA-06]

# Metrics
duration: 3min
completed: 2026-03-17
---

# Phase 2 Plan 01: AppSettings v2 Data Model Summary

**CornerAction enum and per-corner Dictionary<HotCorner, CornerAction> replace the single Corner property, with a LegacyCorner nullable hook for zero-downtime v1 JSON migration**

## Performance

- **Duration:** ~3 min
- **Started:** 2026-03-17T05:45:00Z
- **Completed:** 2026-03-17T05:45:55Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments
- Added `CornerAction` enum (Disabled, TaskView, ShowDesktop, ActionCenter) with JsonStringEnumConverter
- Replaced `Corner` property with `CornerActions` dictionary covering all four HotCorner values
- Added `LegacyCorner` nullable property with `[JsonPropertyName("Corner")]` — reads v1 "Corner" field on deserialize, writes nothing when null
- Added `DefaultCornerActions()` static factory returning all-Disabled dictionary for safe initialization

## Task Commits

Each task was committed atomically:

1. **Task 1: Replace AppSettings.Corner with CornerActions dictionary and add CornerAction enum** - `57eb792` (feat)

**Plan metadata:** _(docs commit pending)_

## Files Created/Modified
- `WindowsHotSpot/Config/AppSettings.cs` - v2 settings model: CornerAction enum, CornerActions dictionary, LegacyCorner migration hook, DefaultCornerActions() factory

## Decisions Made
- CornerAction enum placed in the same file as HotCorner — both are settings-layer types, co-location keeps consumers needing only one using/namespace reference
- LegacyCorner annotated `[JsonIgnore(Condition = WhenWritingNull)]` so re-saving after migration drops the old field cleanly
- `DefaultCornerActions()` is `public static` (not private) so ConfigManager can call it directly during MigrateV1() to initialize the new dictionary from v1 data

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered
- Build errors in `HotSpotApplicationContext.cs` and `SettingsForm.cs` are expected — those files still reference the removed `Corner` property and are updated in Plan 04 per the plan's explicit instruction.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- AppSettings.cs is the foundation type for all Phase 2 work — Plans 02, 03, 04 can proceed
- ConfigManager migration (Plan 02) reads `LegacyCorner` to detect v1 files
- ActionDispatcher (Plan 03) uses `CornerAction` enum as its dispatch input type
- HotSpotApplicationContext / SettingsForm (Plan 04) will resolve the current build errors by consuming `CornerActions` dictionary

## Self-Check: PASSED

- FOUND: WindowsHotSpot/Config/AppSettings.cs
- FOUND: .planning/phases/02-config-foundation/02-01-SUMMARY.md
- FOUND: commit 57eb792 (feat(02-01): evolve AppSettings to v2 data model)

---
*Phase: 02-config-foundation*
*Completed: 2026-03-17*
