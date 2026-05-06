---
gsd_state_version: 1.0
milestone: v1.6
milestone_name: Window Transparency
status: in-progress
stopped_at: ""
last_updated: "2026-05-06"
last_activity: "2026-05-06 — Phase 11 Plan 01 executed: WindowTransparencyHandler implemented"
progress:
  total_phases: 2
  completed_phases: 1
  total_plans: 1
  completed_plans: 1
  percent: 50
---

# State: WindowsHotSpot

## Project Reference

See: .planning/PROJECT.md (updated 2026-05-06 after v1.5 milestone)

**Core value:** The mouse hot corner fires the right action reliably every time, on any screen, with zero friction.
**Current focus:** v1.6 Window Transparency — Ctrl+Alt+Shift+scroll to set window opacity

## Current Position

Phase: 12 — (next phase)
Plan: —
Status: Phase 11 complete; ready for Phase 12
Last activity: 2026-05-06 — Phase 11 Plan 01 complete

```
Progress: [██████████░░░░░░░░░░] 50% (1/2 phases)
```

## Accumulated Context

### Decisions

All decisions logged in PROJECT.md Key Decisions table.

**Phase 8:**
- Double-click detection in HookManager; `GetDoubleClickTime`/`SM_CXDOUBLECLK` polled live (not cached)
- Tracking state reset after detection; suppressed clicks never feed tracking state

**Phase 9:**
- `ScrollResizeHandler` mirrors `WindowDragHandler` keyboard hook pattern exactly
- Cursor-anchor math: `fx=(cursorX-left)/w, newLeft=cursorX-(fx*nw)`
- `Screen.WorkingArea` for clamping (not `Bounds`) — taskbar excluded
- Per-edge clamping; min-size re-applied after screen clamp

**Phase 10:**
- AOT uses `MouseButtonChanged` (not `MouseDoubleClicked`) — Ctrl+Alt clicks suppressed before detection block
- `_lastDownTime != 0` guard prevents single-click false trigger
- `GetWindowText` for balloon title

**Phase 11:**
- `WindowTransparencyHandler` clones `ScrollResizeHandler` pattern exactly — same hook, different action
- LWA_ALPHA OR'd with existingFlags to preserve LWA_COLORKEY on color-key windows (TRNSP-03)
- VK_LSHIFT case has no `!isInjected` guard — AltGr does not synthesize LShift
- Alpha clamped to 25-255 to prevent window becoming invisible (TRNSP-05)

### Blockers/Concerns

- `Screen.DeviceName` stability as monitor identity key is medium-confidence — validate empirically in next monitor-touching feature
- `IsElevatedProcess` / `IsPhysicallyDown` duplicated across three handlers — candidate for shared utility extraction (deferred to after Phase 12)

## Session Continuity

Last session: 2026-05-06 — Phase 11 Plan 01 complete (WindowTransparencyHandler)
Stopped at: Phase 11 complete — ready for Phase 12
