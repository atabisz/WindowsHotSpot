---
gsd_state_version: 1.0
milestone: v1.5
milestone_name: Window Interactions
status: in-progress
stopped_at: ""
last_updated: "2026-05-05"
last_activity: "2026-05-05 — Phase 10 Plan 01 complete: AlwaysOnTopHandler implemented, NativeMethods extended"
progress:
  total_phases: 3
  completed_phases: 0
  total_plans: 2
  completed_plans: 3
  percent: 50
---

# State: WindowsHotSpot

## Project Reference

See: .planning/PROJECT.md (updated 2026-05-05 for v1.5)

**Core value:** The mouse hot corner fires the right action reliably every time, on any screen, with zero friction.
**Current focus:** v1.5 Window Interactions — scroll-to-resize and always-on-top toggle

## Current Position

Phase: Phase 10 — Always-on-Top (in progress)
Plan: 10-01 (complete)
Status: 10-01 complete; AlwaysOnTopHandler implemented and built; awaiting 10-02 wiring
Last activity: 2026-05-05 — Phase 10 Plan 01 complete: AlwaysOnTopHandler implemented, NativeMethods extended

```
Progress: [██████████░░░░░░░░░░] ~50% (3/6 plans in phase 9-10)
```

## Performance Metrics

**By Phase (v1.2):**

| Phase | Plans | Duration | Files |
|-------|-------|----------|-------|
| 02-config-foundation P01 | 1 | 3 min | 1 |
| 02-config-foundation P02 | 1 | 2 min | 1 |
| 02-config-foundation P03 | 1 | 4 min | 2 |
| 02-config-foundation P04 | 1 | 4 min | 3 |
| 03-detection-pipeline P01 | 1 | 8 min | 2 |
| 03-detection-pipeline P02 | 1 | 2 min | 1 |
| 03-detection-pipeline P03 | 1 | 10 min | 1 |
| 04-custom-shortcut P01 | 1 | 5 min | 4 |
| 04-custom-shortcut P02 | 1 | 1 min | 1 |
| 04-custom-shortcut P03 | 1 | 2 min | 2 |
| 04-custom-shortcut P04 | 1 | 15 min | 3 |

## Accumulated Context

### Decisions

All decisions logged in PROJECT.md Key Decisions table.

**Phase 9 Plan 01:**
- Mirrored WindowDragHandler exactly for ScrollResizeHandler keyboard hook pattern (GC-pin, Install/Dispose, self-heal, AltGr guard)
- No CancelOnRelease in ScrollResizeHandler — no persistent drag state to cancel
- SWP_ASYNCWINDOWPOS without SWP_NOSIZE — resize operation passes new cx/cy dimensions
- Zero-size window guard (w==0 || h==0) placed before fx/fy computation (T-09-04 division-by-zero)

**Phase 9 Plan 02:**
- windowInteractionsGroupTop = windowDragGroupTop + 56 — consistent with 56px group-spacing rhythm
- ScrollResizeStep saved immediately after WindowDragPassThrough in settings-save path
- MouseWheeled unsubscribed before _hookManager.Dispose() in DisposeComponents teardown order

**Phase 10 Plan 01:**
- Double-click tracking uses _lastDownTime != 0 guard — single click never falsely triggers
- AlwaysOnTopHandler constructor takes AppSettings + NotifyIcon for future extensibility and balloon feedback
- SWP_FLAGS: SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE — position/size preserved, no focus steal
- Verbatim copy of keyboard hook lifecycle from ScrollResizeHandler — consistent handler pattern

### Blockers/Concerns

- `Screen.DeviceName` stability as monitor identity key is medium-confidence — validate empirically in next monitor-touching feature (driver reinstall, sleep/wake, RDP)

## Session Continuity

Last session: 2026-05-05 — Phase 10 Plan 01 executed: AlwaysOnTopHandler + NativeMethods extensions
Stopped at: Phase 10 Plan 02 (wire AlwaysOnTopHandler into HotSpotApplicationContext)
