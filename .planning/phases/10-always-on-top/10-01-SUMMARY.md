---
phase: 10-always-on-top
plan: "01"
subsystem: ui
tags: [winforms, pinvoke, keyboard-hook, always-on-top, native-methods]

# Dependency graph
requires:
  - phase: 09-scroll-resize
    provides: ScrollResizeHandler keyboard hook pattern (WH_KEYBOARD_LL, GC-pin, Install/Dispose, self-heal, AltGr guard, IsElevatedProcess)
provides:
  - AlwaysOnTopHandler class with keyboard hook lifecycle, double-click detection, and AOT toggle
  - NativeMethods additions: SWP_NOMOVE, HWND_TOPMOST, HWND_NOTOPMOST, GWL_EXSTYLE, WS_EX_TOPMOST, GetWindowLong
affects:
  - 10-02 (wires AlwaysOnTopHandler into HotSpotApplicationContext and HookManager.MouseButtonChanged)

# Tech tracking
tech-stack:
  added: []
  patterns:
    - Double-click detection via self-tracked _lastDownTime/_lastDownPt against GetDoubleClickTime/GetSystemMetrics
    - SetWindowPos with HWND_TOPMOST/HWND_NOTOPMOST to toggle WS_EX_TOPMOST extended style
    - Same keyboard hook lifecycle pattern as ScrollResizeHandler (verbatim copy)

key-files:
  created:
    - WindowsHotSpot/Core/AlwaysOnTopHandler.cs
  modified:
    - WindowsHotSpot/Native/NativeMethods.cs

key-decisions:
  - "Double-click tracking uses _lastDownTime != 0 guard to skip the first click (prevents false trigger on single click)"
  - "SWP_FLAGS uses SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE — position/size preserved, focus not stolen"
  - "AlwaysOnTopHandler constructor takes AppSettings (for future extension) and NotifyIcon (for balloon feedback)"
  - "Verbatim copy of keyboard hook lifecycle from ScrollResizeHandler — consistent pattern across handlers"

patterns-established:
  - "Handler pattern: keyboard hook (GC-pinned callback) + self-heal modifier state + subscribe to HookManager event"
  - "Toggle state read via GetWindowLong(GWL_EXSTYLE) & WS_EX_TOPMOST before deciding HWND_TOPMOST vs HWND_NOTOPMOST"

requirements-completed: [AOT-01, AOT-02, AOT-03, AOT-04]

# Metrics
duration: 8min
completed: 2026-05-05
---

# Phase 10 Plan 01: Always-on-Top Handler Summary

**AlwaysOnTopHandler with WH_KEYBOARD_LL hook, LCtrl+LAlt double-click detection, and SetWindowPos HWND_TOPMOST/HWND_NOTOPMOST toggle plus GetWindowLong P/Invoke additions to NativeMethods**

## Performance

- **Duration:** 8 min
- **Started:** 2026-05-05T22:23:36Z
- **Completed:** 2026-05-05T22:31:00Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments
- Extended NativeMethods.cs with six new items: SWP_NOMOVE constant, HWND_TOPMOST/HWND_NOTOPMOST sentinel IntPtrs, GWL_EXSTYLE/WS_EX_TOPMOST constants, GetWindowLong P/Invoke
- Created AlwaysOnTopHandler.cs — full keyboard hook lifecycle (GC-pinned callback, Install, Dispose) copied verbatim from ScrollResizeHandler
- OnMouseButtonChanged implements LCtrl+LAlt modifier gate, self-heal after SAS swallows key-ups, double-click timing+distance check using GetDoubleClickTime and SM_CXDOUBLECLK/SM_CYDOUBLECLK
- ToggleAlwaysOnTop reads WS_EX_TOPMOST via GetWindowLong, skips elevated windows via IsElevatedProcess, fires SetWindowPos + tray balloon

## Task Commits

Each task was committed atomically:

1. **Task 1: Add NativeMethods constants and GetWindowLong P/Invoke** - `c38d168` (feat)
2. **Task 2: Implement AlwaysOnTopHandler** - `6560306` (feat)

**Plan metadata:** *(docs commit follows)*

## Files Created/Modified
- `WindowsHotSpot/Native/NativeMethods.cs` - Added SWP_NOMOVE, HWND_TOPMOST, HWND_NOTOPMOST, GWL_EXSTYLE, WS_EX_TOPMOST, GetWindowLong P/Invoke
- `WindowsHotSpot/Core/AlwaysOnTopHandler.cs` - New: keyboard hook handler for Ctrl+Alt double-click AOT toggle

## Decisions Made
- Double-click tracking uses `_lastDownTime != 0` guard so a single click never falsely triggers (only second click within time+distance fires)
- `AlwaysOnTopHandler(AppSettings settings, NotifyIcon trayIcon)` — AppSettings included for future extensibility (e.g., configurable gesture)
- SWP_FLAGS: `SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE` — position and size preserved, no focus steal on toggle
- Verbatim copy of keyboard hook lifecycle from ScrollResizeHandler — establishes consistent handler pattern across all Window Interactions handlers

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## User Setup Required

None - no external service configuration required.

## Next Phase Readiness

- AlwaysOnTopHandler is ready; wiring into HotSpotApplicationContext and HookManager.MouseButtonChanged subscription is Plan 10-02
- No blockers

---
*Phase: 10-always-on-top*
*Completed: 2026-05-05*
