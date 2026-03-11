---
phase: 01-core-detection-and-system-tray
plan: 01
subsystem: core
tags: [dotnet, winforms, pinvoke, mouse-hook, win32, dpi, sendinput]

requires: []
provides:
  - net10.0-windows WinForms project with Per-Monitor V2 DPI manifest
  - All P/Invoke declarations for WH_MOUSE_LL hook and SendInput
  - HookManager: global mouse hook with pinned delegate and IDisposable cleanup
  - CornerDetector: 3-state dwell machine with drag suppression and multi-monitor support
  - ActionTrigger: atomic Win+Tab via 4 SendInput INPUT structs
affects:
  - 01-02: HotSpotApplicationContext wires HookManager and CornerDetector

tech-stack:
  added: [net10.0-windows, WinForms, user32.dll P/Invoke, kernel32.dll P/Invoke]
  patterns:
    - ApplicationContext pattern (no main form, no taskbar button)
    - Pinned delegate field to prevent GC collection of hook callback
    - System.Windows.Forms.Timer for dwell (UI thread, not threadpool)
    - 3-state machine (Idle/Dwelling/Triggered) preventing double-trigger

key-files:
  created:
    - WindowsHotSpot/WindowsHotSpot.csproj
    - WindowsHotSpot/app.manifest
    - WindowsHotSpot/Program.cs
    - WindowsHotSpot/Native/NativeMethods.cs
    - WindowsHotSpot/Core/HookManager.cs
    - WindowsHotSpot/Core/ActionTrigger.cs
    - WindowsHotSpot/Core/CornerDetector.cs
  modified: []

key-decisions:
  - "Used NoWarn WFO0003 to suppress redundant DPI warning: manifest needed for downlevel compat AND for unvirtualized hook coordinates; csproj property sets API-level mode. Both are intentional per research Pattern 6."
  - "3-state machine (Idle/Dwelling/Triggered) instead of 4-state (with Cooldown): Triggered state already prevents re-fire until mouse exits zone, making Cooldown redundant."
  - "ActionTrigger.SendTaskView() called only from Timer Tick (UI thread), never from hook callback -- keeps callback trivially fast (<300ms requirement)"

patterns-established:
  - "P/Invoke pattern: all Win32 declarations centralized in Native/NativeMethods.cs static class"
  - "Hook safety: delegate stored as readonly class field, IDisposable always calls UnhookWindowsHookEx"
  - "Timer pattern: System.Windows.Forms.Timer for anything interacting with WinForms or SendInput"

requirements-completed:
  - CORE-01
  - CORE-02
  - CORE-03
  - CORE-04
  - CORE-05
  - CORE-06

duration: 5min
completed: 2026-03-11
---

# Phase 01 Plan 01: Project Scaffolding and Core Detection Engine Summary

**net10.0-windows WinForms project with global WH_MOUSE_LL hook (pinned delegate), 3-state dwell state machine, multi-monitor corner detection, drag suppression, and atomic Win+Tab SendInput trigger**

## Performance

- **Duration:** ~5 min
- **Started:** 2026-03-11T00:45:27Z
- **Completed:** 2026-03-11T00:48:41Z
- **Tasks:** 2
- **Files modified:** 7

## Accomplishments

- Created net10.0-windows WinForms project with Per-Monitor V2 DPI in both manifest and csproj (correct downlevel coverage)
- NativeMethods.cs with all 5 P/Invoke signatures (SetWindowsHookEx, UnhookWindowsHookEx, CallNextHookEx, GetModuleHandle, SendInput), structs (POINT, MSLLHOOKSTRUCT, INPUT, InputUnion, KEYBDINPUT), and all constants
- HookManager with pinned delegate field (prevents GC), MouseMoved and MouseButtonChanged events, IDisposable cleanup
- CornerDetector with Idle/Dwelling/Triggered state machine, System.Windows.Forms.Timer dwell, drag suppression on button-down, multi-monitor zone check via Screen.AllScreens, re-arm on zone exit
- ActionTrigger with 4 atomic SendInput structs (Win down, Tab down, Tab up, Win up)

## Task Commits

1. **Task 1: Create project scaffolding with P/Invoke and DPI manifest** - `d1926c6` (feat)
2. **Task 2: Implement core detection engine** - `f9e48a6` (feat)

## Files Created/Modified

- `WindowsHotSpot/WindowsHotSpot.csproj` - net10.0-windows, WinExe, PerMonitorV2, manifest ref, NoWarn WFO0003
- `WindowsHotSpot/app.manifest` - Per-Monitor V2 dpiAwareness + downlevel dpiAware, asInvoker, Windows 10 GUID
- `WindowsHotSpot/Program.cs` - [STAThread] entry, Application.Run() placeholder pending plan-02
- `WindowsHotSpot/Native/NativeMethods.cs` - All P/Invoke declarations, structs, and constants
- `WindowsHotSpot/Core/HookManager.cs` - Global mouse hook with pinned delegate, MouseMoved/MouseButtonChanged events, IDisposable
- `WindowsHotSpot/Core/ActionTrigger.cs` - Static SendTaskView() with 4 atomic SendInput structs
- `WindowsHotSpot/Core/CornerDetector.cs` - 3-state dwell machine, drag suppression, multi-monitor zone check

## Decisions Made

- Used `NoWarn WFO0003` to suppress warning about DPI settings in both manifest and csproj. Both are intentional: the manifest declares DPI awareness at the OS level (required for unvirtualized hook coordinates and downlevel compat), while the csproj property sets the WinForms API-level mode. Per research Pattern 6 this dual approach is correct.
- Implemented 3-state machine (Idle/Dwelling/Triggered) rather than 4-state (with Cooldown). Triggered already prevents re-fire until mouse exits zone; Cooldown adds complexity with no benefit.
- ActionTrigger.SendTaskView() is called only from the Timer Tick handler (runs on UI thread), never from HookCallback. This ensures the hook callback remains trivially fast.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Suppressed WFO0003 warning for intentional dual DPI configuration**
- **Found during:** Task 1 (build verification)
- **Issue:** Build produced warning WFO0003 because DPI settings appear in both app.manifest and ApplicationHighDpiMode csproj property. While the warning is technically correct that they overlap, both are required per research Pattern 6 for full coverage.
- **Fix:** Added `<NoWarn>WFO0003</NoWarn>` to csproj to suppress the informational warning
- **Files modified:** WindowsHotSpot/WindowsHotSpot.csproj
- **Verification:** Build produces 0 warnings, 0 errors
- **Committed in:** d1926c6 (Task 1 commit)

---

**Total deviations:** 1 auto-fixed (1 warning suppression)
**Impact on plan:** Minimal. Warning suppression was required to achieve the "zero warnings" build goal while keeping the intentionally correct dual DPI configuration.

## Issues Encountered

None.

## Next Phase Readiness

- All 6 CORE requirements have implementing code: CORE-01 through CORE-06
- Detection engine ready to wire into HotSpotApplicationContext in Plan 02
- Plan 02 must: create HotSpotApplicationContext, wire _hookManager.MouseMoved -> _cornerDetector.OnMouseMoved, wire _hookManager.MouseButtonChanged -> _cornerDetector.OnMouseButtonChanged, install hook, add NotifyIcon + ContextMenuStrip, update Program.cs

---
*Phase: 01-core-detection-and-system-tray*
*Completed: 2026-03-11*
