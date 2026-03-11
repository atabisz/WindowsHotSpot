---
phase: 01
status: human_needed
updated: 2026-03-11
---

# Phase 1: Core Detection and System Tray — Verification

**Phase Goal:** User can move their mouse to a screen corner and reliably trigger Task View, with the app running silently in the system tray.

## Automated Checks

### Build

- [x] `dotnet build` succeeds with 0 errors, 0 warnings

### Requirement Traceability

All 12 Phase 1 requirements accounted for across 2 plans:

| Requirement | Plan | Code Location | Verified |
|-------------|------|---------------|---------|
| CORE-01 (Win+Tab trigger) | 01-01 | ActionTrigger.SendTaskView(), CornerDetector.OnDwellComplete | ✓ Code present |
| CORE-02 (multi-monitor) | 01-01 | CornerDetector.IsInAnyCornerZone iterates Screen.AllScreens | ✓ Code present |
| CORE-03 (300ms dwell) | 01-01 | _dwellTimer.Interval = _dwellDelay (300), System.Windows.Forms.Timer | ✓ Code present |
| CORE-04 (drag suppression) | 01-01 | _isButtonDown check in Idle/Dwelling states; OnMouseButtonChanged cancels Dwelling | ✓ Code present |
| CORE-05 (re-arm on leave) | 01-01 | Triggered -> Idle when !inZone | ✓ Code present |
| CORE-06 (hook cleanup) | 01-01, 01-02 | HookManager.Dispose() calls UnhookWindowsHookEx; wired in ApplicationExit + Dispose | ✓ Code present |
| TRAY-01 (no taskbar button) | 01-02 | ApplicationContext with no MainForm | ✓ Code present |
| TRAY-02 (tray icon) | 01-02 | NotifyIcon with Visible=true | ✓ Code present |
| TRAY-03 (Settings/About/Quit) | 01-02 | ContextMenuStrip with 3 items + separator | ✓ Code present |
| TRAY-04 (Settings placeholder) | 01-02 | OnSettingsClick: MessageBox.Show | ✓ Code present |
| TRAY-05 (About dialog) | 01-02 | OnAboutClick: MessageBox.Show with v0.1.0 info | ✓ Code present |
| TRAY-06 (Quit exits) | 01-02 | OnQuitClick: ExitThread(); Dispose sets Visible=false | ✓ Code present |

### Must-Have Truths Check

| Truth | Verified |
|-------|---------|
| NativeMethods has all P/Invoke for WH_MOUSE_LL hook, SendInput, GetModuleHandle | ✓ 5 DllImport declarations confirmed |
| HookManager installs global low-level mouse hook and fires MouseMoved and MouseButtonChanged events | ✓ SetWindowsHookEx call, both events present |
| HookManager stores delegate in class-level field to prevent GC | ✓ `private readonly NativeMethods.LowLevelMouseProc _hookCallback` |
| HookManager implements IDisposable and calls UnhookWindowsHookEx | ✓ Dispose() confirmed |
| CornerDetector checks mouse position against all connected screens | ✓ `foreach (var screen in Screen.AllScreens)` |
| CornerDetector implements 3-state dwell timer state machine | ✓ Idle/Dwelling/Triggered states in switch |
| CornerDetector suppresses dwell when mouse button is held down | ✓ `_isButtonDown` check in Idle; cancel in Dwelling |
| ActionTrigger sends Win+Tab via 4 atomic SendInput INPUT structs | ✓ 4 INPUT array with SendInput call |
| App manifest declares Per-Monitor V2 DPI awareness | ✓ dpiAwareness=PerMonitorV2 in app.manifest |
| App runs with no taskbar button visible | ✓ No MainForm in ApplicationContext |
| System tray icon appears when app starts | ✓ NotifyIcon.Visible=true in constructor |
| Right-clicking tray icon shows context menu with Settings, About, and Quit | ✓ ContextMenuStrip with all items |

### Commit Verification

| Plan | Commits | Status |
|------|---------|--------|
| 01-01 | d1926c6 (feat), f9e48a6 (feat), 5fe8521 (docs) | ✓ 3 commits |
| 01-02 | 36e1dbc (feat), b833573 (docs) | ✓ 2 commits |

## Human Verification Required

The following must-haves from the phase goal require a live running app to verify:

### Items for Human Testing

1. **Hot corner triggers Task View** (CORE-01)
   - Run: `cd C:\src\WindowsHotSpot\WindowsHotSpot && dotnet run`
   - Move mouse to top-left corner of primary monitor, hold for ~300ms
   - Expected: Task View opens (same as Win+Tab)

2. **Drag suppression works** (CORE-04)
   - Hold left mouse button down, drag mouse to top-left corner, hold 1+ seconds
   - Expected: Task View does NOT open

3. **Re-arming works** (CORE-05)
   - Trigger Task View (step 1), press Escape to close
   - Move mouse away from corner, then return to corner and hold
   - Expected: Task View opens again

4. **No taskbar button** (TRAY-01)
   - After `dotnet run`, check taskbar
   - Expected: No WindowsHotSpot button in taskbar

5. **Tray icon visible** (TRAY-02)
   - Check system tray area (may be in overflow)
   - Expected: Icon visible

6. **Quit cleanly exits** (TRAY-06)
   - Right-click tray icon -> Quit
   - Expected: App exits, tray icon disappears

---

**Automated score:** 12/12 must-haves verified in code
**Human verification:** 6 items require live app testing
**Recommendation:** Run `dotnet run` and test the above scenarios before marking phase complete
