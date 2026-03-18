---
phase: 02-config-foundation
plan: 03
subsystem: core
tags: [csharp, sendinput, dispatch, enum, winapi]

# Dependency graph
requires:
  - 02-01  # CornerAction enum from AppSettings.cs
provides:
  - ActionDispatcher.Dispatch(CornerAction) routing all four actions to SendInput
  - VK_D = 0x44 and VK_A = 0x41 constants in NativeMethods
affects:
  - 02-04 (CornerDetector/HotSpotApplicationContext will call ActionDispatcher.Dispatch)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "4-struct atomic SendInput for Win+Key combos (Win+D, Win+A)"
    - "Single dispatch entry point routing enum to Win32 actions"
    - "Reuse ActionTrigger.SendTaskView() rather than duplicating Win+Tab logic"

key-files:
  created:
    - WindowsHotSpot/Core/ActionDispatcher.cs
  modified:
    - WindowsHotSpot/Native/NativeMethods.cs

key-decisions:
  - "MakeKeyInput helper intentionally duplicated from ActionTrigger (two private call sites — not worth a shared helper in Phase 2)"
  - "Disabled case is explicit break with comment, not default/throw — callers can safely pass Disabled"

requirements-completed: [CRNA-02, CRNA-03, CRNA-04, CRNA-06]

# Metrics
duration: 4min
completed: 2026-03-17
---

# Phase 2 Plan 03: ActionDispatcher Summary

**Static dispatch class routing CornerAction enum to Win+D / Win+A / Win+Tab SendInput sequences, with VK_D and VK_A added to NativeMethods**

## Performance

- **Duration:** ~4 min
- **Started:** 2026-03-17T05:45:00Z
- **Completed:** 2026-03-17T05:49:37Z
- **Tasks:** 2
- **Files modified:** 1
- **Files created:** 1

## Accomplishments

- Added `VK_D = 0x44` and `VK_A = 0x41` to the Input constants block in NativeMethods.cs
- Created `ActionDispatcher.cs` with static `Dispatch(CornerAction)` method covering all four enum values
- TaskView delegates to `ActionTrigger.SendTaskView()` — no duplication of Win+Tab logic
- ShowDesktop sends Win+D via `SendWinKey(VK_D)` — 4-struct atomic SendInput
- ActionCenter sends Win+A via `SendWinKey(VK_A)` — 4-struct atomic SendInput
- Disabled is explicit no-op (`break` with comment)
- All `SendInput` cbSize values use `Marshal.SizeOf<NativeMethods.INPUT>()` — not hardcoded

## Task Commits

Each task was committed atomically:

1. **Task 1: Add VK_D and VK_A constants to NativeMethods** - `8bb9af0` (feat)
2. **Task 2: Create ActionDispatcher.cs** - `0f955a8` (feat)

## Files Created/Modified

- `WindowsHotSpot/Native/NativeMethods.cs` - added VK_D and VK_A virtual key constants after VK_TAB
- `WindowsHotSpot/Core/ActionDispatcher.cs` - new static dispatch class routing CornerAction to SendInput

## Decisions Made

- `MakeKeyInput` private helper is intentionally duplicated from ActionTrigger — two private call sites in Phase 2 don't justify a shared static helper in NativeMethods; Phase 3 can refactor if a third call site emerges
- Disabled is `break` (explicit no-op) rather than `default`/`throw` — CornerDetector will pass Disabled when a corner has no action configured, and a silent no-op is the correct behavior

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

- Build errors in `HotSpotApplicationContext.cs` and `SettingsForm.cs` are expected — those files still reference the removed `Corner` property. Resolved in Plan 04 per plan's explicit instruction. No errors in ActionDispatcher.cs or NativeMethods.cs.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- ActionDispatcher is the single place where CornerAction maps to SendInput behavior
- Plan 04 (SettingsForm + HotSpotApplicationContext) calls ActionDispatcher.Dispatch() after resolving the Settings.Corner build errors

## Self-Check: PASSED

- FOUND: WindowsHotSpot/Core/ActionDispatcher.cs
- FOUND: WindowsHotSpot/Native/NativeMethods.cs (VK_D and VK_A)
- FOUND: commit 8bb9af0 (feat(02-03): add VK_D and VK_A constants to NativeMethods)
- FOUND: commit 0f955a8 (feat(02-03): create ActionDispatcher routing CornerAction to SendInput)

---
*Phase: 02-config-foundation*
*Completed: 2026-03-17*
