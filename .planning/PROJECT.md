# WindowsHotSpot

## What This Is

A lightweight Windows system tray application written in C# (.NET 10 WinForms) that triggers Task View (Win+Tab) when the user moves their mouse to a configured corner of any screen and holds it there for a configurable dwell period. It runs silently in the background with no taskbar presence, giving macOS-style hot corner functionality to Windows. Distributed as a self-contained single-file installer that requires no .NET runtime and no admin elevation.

## Core Value

The mouse hot corner must reliably trigger Task View on any screen without accidental activations.

## Requirements

### Validated

- ✓ Mouse moved to configured corner of any screen triggers Task View (Win+Tab) — v1.0
- ✓ Configurable: which corner (TL, TR, BL, BR) and zone size in pixels — v1.0
- ✓ Configurable dwell delay in milliseconds before triggering (default 300ms) — v1.0
- ✓ App has no taskbar button (hidden from taskbar) — v1.0
- ✓ System tray icon with Quit, About, and Open Settings menu items — v1.0
- ✓ Settings persisted to JSON config file next to the exe — v1.0
- ✓ Settings dialog allows changing corner, zone size, and dwell delay — v1.0
- ✓ Settings dialog has "Start with Windows" checkbox (uses HKCU Run registry key) — v1.0
- ✓ Multi-monitor aware: hot corner detection works on all connected screens — v1.0
- ✓ Distributed as a self-contained single-file Inno Setup installer — v1.0

### Active

(None — start /gsd:new-milestone to define v1.1 requirements)

### Out of Scope

- Multiple simultaneous hot corners — deferred to v2 (single corner covers the core use case)
- Configurable action (always Task View, not other shortcuts) — always Task View for v1; scope control
- Mac-style Mission Control grid — Windows Task View is the target
- Per-monitor corner configuration — one global corner setting is sufficient for v1
- Custom action / command execution — security risk surface, deferred
- Dark/light theme matching — WinForms theming is experimental

## Context

**v1.0 shipped: 2026-03-11**

- Language: C# .NET 10 WinForms
- Target platform: Windows 10/11
- Source: 831 LOC across 17 .cs files
- Published: self-contained single-file exe (win-x64, ~45 MB with Inno Setup wrapper)
- Mouse polling: global low-level mouse hook (SetWindowsHookEx WH_MOUSE_LL)
- Task View: Win+Tab via SendInput (4 atomic INPUT structs)
- Settings: JSON file at AppContext.BaseDirectory (compatible with single-file publish)
- Startup: HKCU\Software\Microsoft\Windows\CurrentVersion\Run via Environment.ProcessPath
- Installer: Inno Setup 6.7.1, installs to {localappdata}\WindowsHotSpot, no admin required

## Constraints

- **Tech stack**: C# .NET 10 WinForms — standard for Windows system tray apps
- **No admin required**: App runs without elevation; uses HKCU registry, not HKLM
- **Distribution**: Inno Setup 6 installer producing a single setup.exe
- **No PublishTrimmed**: WinForms is reflection-heavy; trimming is risky; compression alone is sufficient

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| WinForms over WPF | Simpler for system tray + settings dialog, no XAML overhead | ✓ Good — minimal friction |
| JSON config over registry | User preference; easy to inspect and copy | ✓ Good — works well with single-file publish via AppContext.BaseDirectory |
| Global mouse hook | Only reliable way to detect corner across all apps | ✓ Good — hook callback stays under 300ms |
| 3-state machine (Idle/Dwelling/Triggered) | Cooldown state is redundant since Triggered blocks re-fire until zone exit | ✓ Good — simpler and correct |
| System.Windows.Forms.Timer for dwell | Fires on UI thread — safe for SendInput and WinForms; threadpool timers would require Invoke | ✓ Good — no marshalling needed |
| InputUnion must include MOUSEINPUT | Without it, Marshal.SizeOf<INPUT>=28 on 64-bit; Windows expects 40 bytes; SendInput silently returns 0 | ✓ Critical fix — resolved Task View not opening |
| AppContext.BaseDirectory for config path | Assembly.Location returns empty string in single-file publish | ✓ Good — required for single-file compatibility |
| Environment.ProcessPath for startup registry | Returns the actual exe path even in single-file publish | ✓ Good |
| No PublishTrimmed | WinForms uses reflection heavily; trimming causes runtime failures | ✓ Good — compression alone achieves acceptable size |
| Inno Setup installs to {localappdata}\Programs | Admin unavailable for system-wide install; user-local is functionally identical | ✓ Good — no elevation required |
| Per-Monitor V2 DPI manifest + csproj dual config | Both needed: manifest for Windows DPI awareness mode, csproj for WinForms scaling | ✓ Good — multi-monitor coordinates agree |

---
*Last updated: 2026-03-11 after v1.0 milestone*
