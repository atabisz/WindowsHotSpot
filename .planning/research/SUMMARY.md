# Project Research Summary

**Project:** WindowsHotSpot
**Domain:** Windows system tray utility (hot corner trigger for Task View)
**Researched:** 2026-03-11
**Confidence:** HIGH

## Executive Summary

WindowsHotSpot is a lightweight Windows system tray application that detects when the mouse cursor dwells in a screen corner and triggers Task View (Win+Tab). This is a well-understood pattern with direct precedent in at least 6 open-source competitors. The recommended approach is a zero-dependency .NET 10 WinForms app using `ApplicationContext` (no main window), a global low-level mouse hook (`WH_MOUSE_LL`) via P/Invoke, and `SendInput` to simulate the Win+Tab keystroke. The entire application runs on a single STA thread with the WinForms message loop serving as both the hook's message pump and the timer/UI dispatch mechanism.

The core technical risk is the Windows hook timeout constraint: if the `LowLevelMouseProc` callback takes too long to return (>300ms default, 1000ms hard cap on Win10 1709+), Windows silently removes the hook with no notification, and the app stops working while appearing healthy in the tray. This dictates the entire architecture -- the callback must be trivially fast, and the app must set Per-Monitor V2 DPI awareness via manifest so that hook coordinates and `Screen.Bounds` agree across mixed-DPI multi-monitor setups. A secondary risk is the UIPI limitation where `SendInput` may be blocked when an elevated application is in the foreground, though empirical testing suggests Win+Tab may bypass this since it targets the shell.

The stack requires zero NuGet dependencies -- all needed APIs (System.Text.Json, Microsoft.Win32.Registry, System.Windows.Forms, P/Invoke) are inbox in .NET 10. Distribution should use self-contained single-file publish wrapped in an Inno Setup installer. The feature set for v1 is well-defined by competitor analysis: single configurable corner, dwell delay, zone size, drag suppression, multi-monitor support, system tray with settings dialog, and Start with Windows. Multiple corners, custom actions, and full-screen detection are explicit v2 deferrals.

## Key Findings

### Recommended Stack

The stack is intentionally minimal: .NET 10 LTS, WinForms (not WPF -- no XAML benefit for a tray app), zero NuGet packages. All functionality comes from inbox .NET libraries and a handful of P/Invoke declarations (~10 lines of DllImport signatures). Publishing as a self-contained single-file exe eliminates runtime dependency issues on target machines. Inno Setup is recommended over WiX for the installer due to simpler scripting and single setup.exe output.

**Core technologies:**
- **.NET 10.0 LTS (WinForms):** Runtime and UI framework -- current LTS, all needed APIs inbox, no external dependencies
- **P/Invoke (user32.dll, kernel32.dll):** Global mouse hook and SendInput -- no managed alternative exists for system-wide mouse monitoring
- **System.Text.Json:** Settings persistence -- inbox, no NuGet required
- **Inno Setup 6.x:** Installer -- simpler than WiX, produces single setup.exe, supports no-admin install

### Expected Features

**Must have (table stakes):**
- Corner detection with dwell timer triggering Task View (the core value proposition)
- System tray icon with context menu (Quit, Settings)
- Configurable corner selection (TL/TR/BL/BR)
- Configurable dwell delay (default 150-300ms) and zone size (default 5-10px)
- Drag suppression (ignore mouse-down to prevent accidental triggers)
- Multi-monitor awareness (Screen.AllScreens corner calculation)
- Settings persistence via JSON file
- Start with Windows toggle (HKCU Run key)
- Settings dialog (WinForms, does not need to be fancy)

**Should have (competitive):**
- Full-screen app detection (auto-disable during games/video)
- Quick enable/disable toggle via tray icon click
- Visual countdown indicator during dwell

**Defer (v2+):**
- Multiple simultaneous hot corners (explicitly scoped out in PROJECT.md)
- Configurable trigger actions beyond Task View (explicitly scoped out)
- Dark/light theme matching (WinForms theming is experimental)
- Custom action/command execution
- Edge triggers (high false-positive risk)

### Architecture Approach

The app follows the standard "invisible tray app with hook" pattern. `Program.Main` calls `Application.Run(new HotSpotApplicationContext())` where the ApplicationContext owns all components and their lifetimes. The mouse hook callback fires on the UI thread via the message loop, feeds coordinates to CornerDetector, which manages a `System.Windows.Forms.Timer` for dwell delay and fires ActionTrigger.SendTaskView() when the dwell completes. Everything runs on the single STA thread -- no background threads, no cross-thread synchronization needed.

**Major components:**
1. **HotSpotApplicationContext** -- owns app lifetime, creates/disposes all components, no main form
2. **HookManager** -- installs/uninstalls WH_MOUSE_LL via P/Invoke, exposes MouseMoved event
3. **CornerDetector** -- checks if mouse is in hot corner zone on any screen, manages dwell timer state machine (Idle/Dwelling/Triggered/Cooldown)
4. **ActionTrigger** -- sends Win+Tab via SendInput (4 INPUT structs sent atomically)
5. **ConfigManager** -- loads/saves AppSettings from JSON file next to exe
6. **SettingsForm** -- WinForms dialog for corner, zone size, dwell delay, startup toggle
7. **StartupManager** -- reads/writes HKCU Run registry key
8. **NativeMethods** -- centralized P/Invoke declarations (DllImport, structs, constants)

### Critical Pitfalls

1. **Hook timeout causes silent removal** -- Windows removes the hook if the callback exceeds ~300ms-1000ms with zero notification. Keep the callback trivially fast: read coordinates, bounds-check, set flag, return. Never do I/O, allocation, or UI work in the callback.
2. **GC collects the hook delegate** -- The delegate passed to SetWindowsHookEx must be stored in a class-level field (not a local/lambda) to prevent garbage collection, which would cause a crash or silent hook death.
3. **DPI coordinate mismatch on multi-monitor** -- Without Per-Monitor V2 DPI awareness declared in the app manifest, `Screen.Bounds` returns virtualized coordinates that disagree with the hook's raw physical coordinates. This silently breaks corner detection on secondary monitors with different scaling.
4. **Double-trigger of Task View** -- Without a state machine that requires the mouse to leave the zone before re-arming, the dwell timer fires repeatedly. A second Win+Tab closes Task View, making it appear as a flicker or no-op.
5. **Hook not unregistered on crash** -- Orphaned hooks cause system-wide mouse lag. Must implement cleanup in ApplicationExit, try/finally, UnhandledException handler, and IDisposable.

## Implications for Roadmap

Based on research, suggested phase structure:

### Phase 1: Core Hook and Detection Loop
**Rationale:** This is the riskiest phase (P/Invoke, hook timeout constraint, DPI awareness) and everything else depends on it. Architecture research explicitly recommends building and validating this first with hard-coded settings before adding any UI.
**Delivers:** A working tray app that detects mouse in top-left corner and triggers Task View, with proper cleanup on exit.
**Addresses:** Corner detection, dwell timer, Task View trigger, drag suppression, multi-monitor awareness, system tray icon with Quit menu.
**Avoids:** Hook timeout (Pitfall 1), delegate GC (Pitfall 2), DPI mismatch (Pitfall 4), double-trigger (Pitfall 7), hook cleanup (Pitfall 5).
**Components:** Program, HotSpotApplicationContext, HookManager, CornerDetector, ActionTrigger, NativeMethods, NotifyIcon with basic context menu, app.manifest (Per-Monitor V2 DPI).

### Phase 2: Settings and Configuration
**Rationale:** With the core loop validated, extract hard-coded values into user-configurable settings. This phase has low risk -- standard WinForms dialog and JSON serialization.
**Delivers:** Settings dialog, JSON persistence, Start with Windows toggle, configurable corner/zone/delay.
**Addresses:** Configurable corner selection, zone size, dwell delay, settings persistence, Start with Windows, settings dialog.
**Avoids:** Registry path staleness (Pitfall 10) by rewriting path on startup, blurry settings dialog (Pitfall 9) by using AutoScaleMode.Dpi.
**Components:** ConfigManager, AppSettings, SettingsForm, StartupManager.

### Phase 3: Polish and Distribution
**Rationale:** Packaging and distribution come last because they require all components to be complete. Self-contained publish eliminates runtime dependency issues.
**Delivers:** Installable setup.exe, self-contained single-file exe, enable/disable toggle, app icon.
**Addresses:** Installer, self-contained publish, enable/disable tray toggle (if time permits).
**Avoids:** Missing .NET runtime (Pitfall 12), installer UAC confusion (Pitfall 14).
**Components:** Inno Setup script, publish profile, icon resources.

### Phase Ordering Rationale

- Phase 1 first because it contains all the P/Invoke risk and the hook timeout constraint that dictates architecture. If this fails, nothing else matters.
- Phase 2 second because settings are low-risk, use standard WinForms patterns, and require the core loop to be stable.
- Phase 3 last because packaging requires a complete app and is independent of feature work.
- Drag suppression belongs in Phase 1 (not Phase 2) because it is a usability-critical behavior baked into the hook callback, not a setting.
- Multi-monitor support belongs in Phase 1 because the Per-Monitor V2 manifest and coordinate math must be correct from the start -- retrofitting DPI awareness is a rewrite.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 1:** The UIPI/SendInput interaction with Win+Tab specifically needs empirical testing. Research is confident about the constraint but uncertain whether Win+Tab (which targets Explorer shell) is actually affected. Test early.
- **Phase 1:** Whether to use a dedicated hook thread vs. the UI thread. Architecture research recommends starting with the UI thread (simpler) but Pitfalls research flags message pump starvation as a risk if the settings dialog blocks the pump. Resolution: show SettingsForm as a separate top-level window that does not block the message loop, or use a dedicated hook thread.

Phases with standard patterns (skip research-phase):
- **Phase 2:** JSON settings, WinForms dialog, registry Run key -- all well-documented, established patterns with code examples in ARCHITECTURE.md.
- **Phase 3:** Inno Setup scripting and dotnet publish are thoroughly documented. No research needed.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | HIGH | Zero NuGet deps, all inbox .NET 10 APIs, verified SDK available locally |
| Features | HIGH | Based on analysis of 6 competing open-source projects with clear consensus |
| Architecture | HIGH | Verified against official Microsoft documentation for all P/Invoke APIs |
| Pitfalls | HIGH | All critical pitfalls sourced from official Microsoft documentation |

**Overall confidence:** HIGH

### Gaps to Address

- **UIPI + Win+Tab empirical testing:** Whether SendInput for Win+Tab is blocked when an elevated app is focused needs hands-on testing in Phase 1. If blocked, the UIAccess manifest approach requires code signing and Program Files installation, which would change the installer requirements.
- **Hook thread vs. UI thread decision:** Architecture research recommends single-thread (simpler), but Pitfalls research warns about message pump starvation from modal dialogs. Must decide in Phase 1 planning whether SettingsForm needs to be non-modal or whether a dedicated hook thread is warranted.
- **WinForms high-DPI limitations:** Microsoft documents WinForms as having "limited" per-monitor DPI scaling. The settings dialog should be kept simple to minimize exposure, but exact behavior at 150%+ scaling needs testing.

## Sources

### Primary (HIGH confidence)
- [SetWindowsHookExW -- Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowshookexw) -- hook installation, timeout behavior, delegate pinning
- [LowLevelMouseProc -- Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc) -- callback constraints, message loop requirement
- [SendInput -- Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput) -- input simulation, UIPI limitations
- [ApplicationContext -- Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.applicationcontext) -- tray app pattern
- [High DPI Desktop Application Development -- Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows) -- DPI awareness modes, coordinate virtualization

### Secondary (MEDIUM confidence)
- GitHub: vhanla/winxcorners (907 stars) -- feature-complete competitor, Delphi implementation reference
- GitHub: flexits/HotCornersWin (127 stars) -- closest tech stack (C# .NET 9), MSI installer reference
- GitHub: taviso/hotcorner (407 stars) -- minimal C implementation, anti-pattern for UX

---
*Research completed: 2026-03-11*
*Ready for roadmap: yes*
