# WindowsHotSpot

## What This Is

A lightweight Windows system tray application written in C# that triggers Task View (Win+Tab) when the user moves their mouse to a configured corner of any screen. It runs silently in the background with no taskbar presence, giving macOS-style hot corner functionality to Windows.

## Core Value

The mouse hot corner must reliably trigger Task View on any screen without accidental activations.

## Requirements

### Validated

(None yet — ship to validate)

### Active

- [ ] Mouse moved to configured corner of any screen triggers Task View (Win+Tab)
- [ ] Configurable: which corner (TL, TR, BL, BR) and zone size in pixels
- [ ] Configurable dwell delay in milliseconds before triggering (default 300ms)
- [ ] App has no taskbar button (hidden from taskbar)
- [ ] System tray icon with Quit, About, and Open Settings menu items
- [ ] Settings persisted to JSON config file next to the exe
- [ ] Settings dialog allows changing corner, zone size, and dwell delay
- [ ] Settings dialog has "Start with Windows" checkbox (uses registry run key)
- [ ] Multi-monitor aware: hot corner detection works on all connected screens
- [ ] Distributed as an installer (NSIS or WiX)

### Out of Scope

- Multiple simultaneous hot corners — single corner keeps it simple
- Configurable action (always Task View, not other shortcuts)
- Mac-style Mission Control grid — Windows Task View is the target
- Per-monitor corner configuration — one global corner setting

## Context

- Target platform: Windows 10/11
- Language: C# (.NET 8+ WinForms)
- Mouse polling via a global low-level mouse hook (SetWindowsHookEx WH_MOUSE_LL)
- Task View triggered by sending Win+Tab keystrokes via SendInput or keybd_event
- Settings stored in a JSON file alongside the executable
- "Start with Windows" via HKCU\Software\Microsoft\Windows\CurrentVersion\Run

## Constraints

- **Tech stack**: C# .NET 8 WinForms — standard for Windows system tray apps
- **No admin required**: App must run without elevation; uses HKCU registry, not HKLM
- **Distribution**: NSIS or WiX installer producing a single setup.exe

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| WinForms over WPF | Simpler for system tray + settings dialog, no XAML overhead | — Pending |
| JSON config over registry | User preference; easy to inspect and copy | — Pending |
| Global mouse hook | Only reliable way to detect corner across all apps | — Pending |

---
*Last updated: 2026-03-11 after initialization*
