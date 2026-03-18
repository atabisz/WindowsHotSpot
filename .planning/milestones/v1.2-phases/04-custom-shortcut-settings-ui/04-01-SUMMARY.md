---
phase: 04-custom-shortcut-settings-ui
plan: 01
subsystem: config-dispatch-pipeline
tags: [custom-shortcut, dispatch, data-model, send-input]
dependency_graph:
  requires: []
  provides: [CustomShortcut-data-model, CornerAction.Custom, SendArbitraryKeys-dispatch]
  affects: [AppSettings, ActionDispatcher, CornerDetector, CornerRouter]
tech_stack:
  added: []
  patterns: [press-all-release-reverse atomic SendInput, optional parameter pipeline pass-through]
key_files:
  created: []
  modified:
    - WindowsHotSpot/Config/AppSettings.cs
    - WindowsHotSpot/Core/ActionDispatcher.cs
    - WindowsHotSpot/Core/CornerDetector.cs
    - WindowsHotSpot/Core/CornerRouter.cs
decisions:
  - CustomShortcut stores ushort[] VirtualKeys (not Keys[] or List<ushort>) — maps 1:1 to KEYBDINPUT.wVk; no masking needed
  - CustomShortcut stores DisplayText rather than regenerating from VKs on load — avoids VK-to-Keys remapping at deserialise time
  - CustomShortcuts lives in MonitorCornerConfig only (not global AppSettings) — per research architecture; global CornerActions fallback retains disconnected-monitor data (MMON-04)
  - SendArbitraryKeys uses press-all/release-reverse pattern matching existing SendWinKey and ActionTrigger.SendTaskView
  - CornerDetector constructor adds optional customShortcut parameter at end — backward-compatible; existing callers unchanged
metrics:
  duration: 5 min
  completed: 2026-03-18
  tasks_completed: 2
  files_modified: 4
requirements:
  - CRNA-05
---

# Phase 4 Plan 01: CustomShortcut Data Model and Dispatch Pipeline Summary

**One-liner:** CustomShortcut record with ushort[] VirtualKeys added to AppSettings, wired through CornerDetector to a new SendArbitraryKeys atomic SendInput helper in ActionDispatcher.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Add CustomShortcut record and CornerAction.Custom to AppSettings | 88f52ec | AppSettings.cs |
| 2 | Extend dispatch pipeline for CornerAction.Custom | 2fd1689 | ActionDispatcher.cs, CornerDetector.cs, CornerRouter.cs |

## What Was Built

### AppSettings.cs

- `CornerAction.Custom` added to enum — serialises as `"Custom"` via the existing `JsonStringEnumConverter` attribute
- `CustomShortcut` sealed record: `ushort[] VirtualKeys`, `string DisplayText`, parameterless constructor for `System.Text.Json` deserialisation
- `MonitorCornerConfig.CustomShortcuts` dictionary (`Dictionary<HotCorner, CustomShortcut>`) — missing key means no shortcut defined

### ActionDispatcher.cs

- `Dispatch(CornerAction action, CustomShortcut? custom = null)` — optional parameter; existing call sites unchanged
- `SendArbitraryKeys(ushort[] vks)` private helper — presses all keys then releases all in reverse order in one atomic `SendInput` call; uses `Marshal.SizeOf<NativeMethods.INPUT>()` (cbSize bug cannot regress)

### CornerDetector.cs

- `_customShortcut` readonly field added
- Constructor gains optional `CustomShortcut? customShortcut = null` parameter
- `OnDwellComplete` passes `_customShortcut` to `ActionDispatcher.Dispatch`

### CornerRouter.cs

- `Rebuild()` inner loop captures `monitorConfig` from `TryGetValue` (renamed from `mc`)
- For `CornerAction.Custom` corners, calls `monitorConfig?.CustomShortcuts.TryGetValue(corner, out custom)` — null-safe; missing entry leaves `custom` null
- `custom` passed to `new CornerDetector(...)` constructor

## Deviations from Plan

None — plan executed exactly as written.

## Self-Check

- [x] `AppSettings.cs` modified with `Custom` enum value, `CustomShortcut` record, `CustomShortcuts` dict
- [x] `ActionDispatcher.cs` modified with `SendArbitraryKeys` and updated `Dispatch` signature
- [x] `CornerDetector.cs` modified with `_customShortcut` field and updated constructor/OnDwellComplete
- [x] `CornerRouter.cs` modified with `CustomShortcuts.TryGetValue` lookup
- [x] Commits 88f52ec and 2fd1689 exist
- [x] Build: 0 errors, 0 warnings
