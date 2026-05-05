---
phase: 05-hook-suppression-infrastructure
plan: 01
subsystem: infra
tags: [winapi, hook, mouse, suppression, csharp]

# Dependency graph
requires: []
provides:
  - HookManager.SuppressionPredicate property (Func<int, bool>?) for selective WM_LBUTTONDOWN/WM_LBUTTONUP suppression
  - Restructured HookCallback with left/right button split and event-before-predicate ordering
affects:
  - 06-window-drag-handler

# Tech tracking
tech-stack:
  added: []
  patterns:
    - "Event-before-predicate: MouseButtonChanged fires before SuppressionPredicate is consulted so consumer state is current when predicate runs"
    - "Selective suppression: returning IntPtr(1) from hook proc consumes the event; target window does not receive it"

key-files:
  created: []
  modified:
    - WindowsHotSpot/Core/HookManager.cs

key-decisions:
  - "SuppressionPredicate consulted only for left-button messages (HOOK-02) — WM_MOUSEMOVE and right-button paths are explicitly excluded"
  - "MouseButtonChanged fires BEFORE predicate is consulted so consumer state is updated when the predicate runs (Pitfall 2 avoidance)"
  - "Null predicate short-circuits to false via ?. operator — pre-Phase-5 call-through behavior is preserved with no branch overhead"

patterns-established:
  - "Hook suppression pattern: return new IntPtr(1) to consume; call CallNextHookEx to pass through"

requirements-completed:
  - HOOK-01
  - HOOK-02

# Metrics
duration: 8min
completed: 2026-05-05
---

# Phase 5 Plan 01: Hook Suppression Infrastructure Summary

**HookManager gains SuppressionPredicate (Func<int, bool>?) enabling Phase 6 WindowDragHandler to suppress WM_LBUTTONDOWN/WM_LBUTTONUP delivery to target windows**

## Performance

- **Duration:** 8 min
- **Started:** 2026-05-05T02:19:00Z
- **Completed:** 2026-05-05T02:27:30Z
- **Tasks:** 2 of 2 (Task 2 checkpoint:human-verify — approved by user 2026-05-05)
- **Files modified:** 1

## Accomplishments
- Added `public Func<int, bool>? SuppressionPredicate { get; set; }` to HookManager with full XML doc comment
- Restructured HookCallback to split left and right button branches (previously grouped together)
- Left-button branch fires MouseButtonChanged before consulting SuppressionPredicate, then returns IntPtr(1) if predicate returns true
- WM_MOUSEMOVE and right-button branches explicitly never consult SuppressionPredicate (HOOK-02 compliance)
- Null predicate or false return preserves pre-Phase-5 pass-through behavior (CallNextHookEx called)
- dotnet build exits 0 with 0 warnings, 0 errors

## Task Commits

Each task was committed atomically:

1. **Task 1: Add SuppressionPredicate and restructure HookCallback** - `f7e96f3` (feat)
2. **Task 2: Manual smoke verification** - checkpoint:human-verify, approved by user (no code commit)

**Plan metadata:** (docs commit — this summary)

## Files Created/Modified
- `WindowsHotSpot/Core/HookManager.cs` - Added SuppressionPredicate property; restructured HookCallback with split button branches and event-before-predicate ordering

## Decisions Made
- SuppressionPredicate uses `?.Invoke(msg) == true` null-conditional pattern — when null it short-circuits to false, preserving pre-Phase-5 behavior without an explicit null check
- Left and right button events now handled in separate `else if` branches to allow selective suppression of left-button only
- XML doc comment documents both the blocking constraint (Pitfall 1: must be fast) and the ordering guarantee (MouseButtonChanged fires first)

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness
- HookManager.SuppressionPredicate is ready for Phase 6 to wire `_hookManager.SuppressionPredicate = ShouldSuppress` with no further modification to HookManager.cs
- Human checkpoint passed: code review confirmed structure and ordering, tray app runs normally without errors
- No blockers

---
*Phase: 05-hook-suppression-infrastructure*
*Completed: 2026-05-05*
