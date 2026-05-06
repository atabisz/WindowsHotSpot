---
phase: 11-window-transparency-handler
plan: 01
subsystem: core
status: completed
tags: [transparency, layered-window, keyboard-hook, p/invoke]
dependency_graph:
  requires: []
  provides: [WindowTransparencyHandler, TransparencyStep, layered-window-pinvokes]
  affects: [WindowsHotSpot.Core, WindowsHotSpot.Native, WindowsHotSpot.Config]
tech_stack:
  added: [SetLayeredWindowAttributes, GetLayeredWindowAttributes, SetWindowLongPtr, WS_EX_LAYERED]
  patterns: [keyboard-hook-modifier-tracking, self-heal-pattern, elevated-process-guard, layered-window-alpha]
key_files:
  created:
    - WindowsHotSpot/Core/WindowTransparencyHandler.cs
  modified:
    - WindowsHotSpot/Native/NativeMethods.cs
    - WindowsHotSpot/Config/AppSettings.cs
decisions:
  - "WindowTransparencyHandler is a near-exact clone of ScrollResizeHandler; same hook pattern, different action (alpha vs resize)"
  - "LWA_ALPHA is OR'd with existing dwFlags to preserve LWA_COLORKEY on color-key windows (TRNSP-03)"
  - "VK_LSHIFT case has no !isInjected guard because AltGr does not synthesize LShift"
  - "Alpha clamped to 25-255 to prevent full invisibility (TRNSP-05)"
metrics:
  duration: ~5 minutes
  completed: 2026-05-06
  tasks_completed: 3
  files_changed: 3
---

# Phase 11 Plan 01: WindowTransparencyHandler Summary

**One-liner:** Layered-window transparency handler using Ctrl+Alt+Shift+scroll with alpha clamped 25-255, preserving LWA_COLORKEY, guarded against elevated and maximized windows.

## What Was Implemented

WindowTransparencyHandler — a self-contained keyboard+mouse handler that adjusts window opacity when the user holds LCtrl+LAlt+LShift and scrolls. The handler:

1. Installs a WH_KEYBOARD_LL hook to track three modifier keys independently
2. Uses the self-heal pattern (each modifier AND'd with IsPhysicallyDown) to recover from SAS key-up swallowing
3. Guards against maximized windows (GUARD-02: SW_SHOWMAXIMIZED check)
4. Guards against elevated windows (GUARD-03: IsElevatedProcess check — UIPI blocks cross-elevation SetLayeredWindowAttributes)
5. Guards against AltGr triggering the gesture (GUARD-01: LLKHF_INJECTED on VK_LCONTROL case)
6. Implements the 5-step layered-window alpha sequence: read exStyle, read existing alpha/flags, compute new alpha, set WS_EX_LAYERED if absent, call SetLayeredWindowAttributes with LWA_ALPHA OR'd into existing flags

## Tasks Completed

| Task | Description | Commit |
|------|-------------|--------|
| 1 | Add VK_LSHIFT, WS_EX_LAYERED, LWA_COLORKEY, LWA_ALPHA constants + SetWindowLongPtr, SetLayeredWindowAttributes, GetLayeredWindowAttributes P/Invokes to NativeMethods.cs; add TransparencyStep=10 to AppSettings.cs | e0fa1bf |
| 2 | Create WindowTransparencyHandler.cs — three-modifier keyboard hook, self-heal, window pipeline with GUARD-02/GUARD-03, 5-step alpha sequence | d5e0c3c |
| 3 | Build verification — dotnet build exits 0, 0 errors, 0 warnings | no-commit |

## Build Result

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## Deviations from Plan

None — plan executed exactly as written.

## Known Stubs

None — WindowTransparencyHandler is a complete, standalone implementation. It is not yet wired into HookManager (that is Phase 12), but the handler itself is fully functional and self-contained.

## Threat Flags

None — all STRIDE mitigations from the plan's threat register are present in the implementation:
- T-11-01: IsElevatedProcess guard before any write
- T-11-02: Math.Clamp(alpha, 25, 255)
- T-11-03: LLKHF_INJECTED guard on VK_LCONTROL
- T-11-04: Self-heal pattern on all three modifiers
- T-11-05: SW_SHOWMAXIMIZED early return
- T-11-06: existingFlags OR'd with LWA_ALPHA to preserve LWA_COLORKEY

## Self-Check: PASSED

- `WindowsHotSpot/Core/WindowTransparencyHandler.cs` exists: FOUND
- Commit e0fa1bf exists: FOUND
- Commit d5e0c3c exists: FOUND
- Build succeeded with 0 errors: CONFIRMED
