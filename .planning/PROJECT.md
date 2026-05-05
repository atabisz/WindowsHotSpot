# WindowsHotSpot

## What This Is

WindowsHotSpot is a Windows system tray app that fires a configurable action when the mouse dwells in a screen corner, and lets users move any window by holding Ctrl+Alt and dragging anywhere on it. Each corner on each monitor can independently trigger Win+Tab (Task View), Show Desktop, Action Center, a recorded custom keystroke, or be disabled. Window drag works on any restored (non-maximized, non-elevated) window without needing to grab the title bar. It runs silently in the background with no taskbar button, configured via a tray icon menu.

## Core Value

The mouse hot corner fires the right action reliably every time, on any screen, with zero friction.

## Requirements

### Validated

- ✓ Mouse dwell in configurable corner triggers Win+Tab — v1.0
- ✓ Configurable corner (TopLeft, TopRight, BottomLeft, BottomRight) — v1.0
- ✓ Configurable dwell delay — v1.0
- ✓ Optional run-at-startup (HKCU Run key) — v1.0
- ✓ System tray icon with Settings / About / Quit — v1.0
- ✓ Settings persisted to JSON — v1.0
- ✓ GitHub Actions CI: builds portable exe + Inno Setup installer — v1.0
- ✓ Single-instance guard: second launch signals first instance to show Settings, exits silently — v1.1
- ✓ Per-corner independent action: disabled, Win+Tab, Show Desktop, Action Center, custom shortcut — v1.2
- ✓ Per-monitor corner configuration using Screen.DeviceName as key — v1.2
- ✓ Hotkey recorder UI (click-to-record custom keystroke, Win-key support) — v1.2
- ✓ Settings UI redesign: 2×2 corner layout + monitor selector + Same-on-all-monitors toggle — v1.2
- ✓ Config schema migration from v1.x to v1.2 with zero data loss — v1.2
- ✓ Live monitor hot-plug: adding/removing monitors updates corners without restart — v1.2
- ✓ Ctrl+Alt+drag moves topmost non-maximized, non-elevated window under cursor without grabbing title bar — v1.4
- ✓ Drag suppressed for maximized windows; AltGr (RCtrl+LAlt) does not trigger drag — v1.4
- ✓ Click suppressed (not forwarded to target app) while dragging — v1.4
- ✓ `HookManager` supports hook suppression (return 1 vs call-through) via `SuppressionPredicate` — v1.4
- ✓ `WindowDragHandler` wired into `HotSpotApplicationContext` as permanent `readonly` field alongside `CornerRouter` — v1.4

### Active

(none — planning next milestone)

### Out of Scope

- Multiple simultaneous triggers — one action per dwell, by design
- Per-corner dwell delay — multiplies settings surface 16x for an edge case; global delay sufficient
- Launch application action — transforms the tool into a general launcher — different product
- Per-app profiles — different product category
- EDID monitor naming — complex SetupAPI; "Display 1 (Primary)" label is sufficient
- Right-click or middle-click drag — left-button only for simplicity
- Resize-by-drag — separate Win32 concern; different UX pattern

## Context

- C# .NET 10 WinForms, single STA thread
- Global low-level mouse hook (WH_MOUSE_LL) fires on UI thread via message loop
- Global low-level keyboard hook (WH_KEYBOARD_LL) in WindowDragHandler tracks LCtrl+LAlt state
- SendInput for key combos: press-all/release-reverse atomic pattern; cbSize must include MOUSEINPUT union (40 bytes)
- Config at `AppContext.BaseDirectory` (works with single-file publish)
- HKCU Run key uses `Environment.ProcessPath`
- Per-Monitor V2 DPI manifest (`app.manifest`)
- Published as single-file self-contained exe; Inno Setup installer installs to `%LOCALAPPDATA%\WindowsHotSpot`
- IPC via `WM_COPYDATA`: hidden `HWND_MESSAGE` `NativeWindow` (`IpcWindow`) receives signal from second instance
- Per-monitor config keyed by `Screen.DeviceName`; missing key defaults to all-corners disabled
- `CornerRouter` owns detector pool; `Rebuild()` recreates on settings change or `DisplaySettingsChanged`
- `KeyRecorderPanel` uses `WH_KEYBOARD_LL` to capture Win key before OS intercepts it
- `WindowDragHandler` uses `WindowFromPoint` → `GetAncestor(GA_ROOT)` → `GetWindowPlacement` → elevation check → `SetWindowPos(SWP_ASYNCWINDOWPOS)` pipeline
- UIPI constraint: drag is skipped for elevated (admin) windows; `IsElevatedProcess` check via `OpenProcessToken/GetTokenInformation` gates `BeginDragAttempt`
- SAS constraint: `GetKeyState` self-heal on each `BeginDragAttempt` prevents modifier-stuck-true lockup after Ctrl+Alt+Del

**Shipped v1.2:** ~1,963 C# source LOC. 3 phases, 11 plans over 7 days.
**Shipped v1.4:** ~2,400 C# source LOC (+449 LOC). 3 phases, 6 plans, single session.

## Constraints

- **Platform**: Windows only — WinForms, Win32 P/Invoke, no cross-platform path
- **Runtime**: .NET 10, single-file publish
- **Privilege**: No UAC — `PrivilegesRequired=lowest` in installer
- **Thread model**: Single STA thread — all hook/timer/SendInput on UI thread

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| WH_MOUSE_LL hook on UI thread | Simplest correct model for WinForms | ✓ Good |
| 4-struct atomic SendInput with MOUSEINPUT in union | Required for correct cbSize (40 bytes) | ✓ Good |
| AppContext.BaseDirectory for config path | Assembly.Location returns empty in single-file publish | ✓ Good |
| Environment.ProcessPath for HKCU Run key | Same reason as above | ✓ Good |
| Inno Setup installer, PrivilegesRequired=lowest | No UAC, installs to %LOCALAPPDATA% | ✓ Good |
| Local\ mutex scope for single-instance | Separate Windows sessions don't block each other | ✓ Good |
| SendMessage (not PostMessage) for IPC | First instance processes WM_COPYDATA before second exits | ✓ Good |
| NativeWindow + HWND_MESSAGE for IpcWindow | Invisible to Alt+Tab; FindWindow still works by title | ✓ Good |
| taskkill /F in installer PrepareToInstall | Tray app ignores graceful close; force-kill needed before file copy | ✓ Good |
| CornerAction enum + per-corner CornerActions dict | Replaces single-corner/single-action; enables independent per-corner dispatch | ✓ Good |
| [JsonPropertyName("Corner")] nullable LegacyCorner | Zero-overhead v1 migration hook — no separate migration file needed | ✓ Good |
| CornerDetector immutable at construction | Rebuilt by CornerRouter on changes; readonly fields enforce contract | ✓ Good |
| Screen.DeviceName as monitor identity key | GDI device string; stable enough for typical use; medium confidence noted | ✓ Good |
| CornerRouter pre-caches Screen.AllScreens in Rebuild() | Keeps OnMouseMoved hot path P/Invoke-free | ✓ Good |
| SystemEvents.DisplaySettingsChanged unsubscribed first in DisposeComponents() | Static events hold strong refs; must release before disposal | ✓ Good |
| CustomShortcut stores ushort[] VirtualKeys + DisplayText | Maps 1:1 to KEYBDINPUT.wVk; avoids VK-to-Keys remapping on deserialise | ✓ Good |
| Panel subclass (not TextBox) for KeyRecorderPanel | TextBox intercepts Tab/Backspace/Enter as editing actions | ✓ Good |
| WH_KEYBOARD_LL in KeyRecorderPanel for Win key | PreviewKeyDown never fires for Win key; OS intercepts before WinForms | ✓ Good |
| ProcessCmdKey returns false (not true) for Escape-while-recording | false propagates to child KeyRecorderPanel; true would consume it at form level | ✓ Good |
| Same-on-all-monitors toggle propagates one config to all screens on save | Avoids repeated data entry on symmetric multi-monitor setups | ✓ Good |
| SuppressionPredicate as `Func<int, bool>?` on HookManager | Consumer registers inline; null short-circuits to false with `?.Invoke == true` — no branch overhead | ✓ Good |
| MouseButtonChanged fires BEFORE SuppressionPredicate is consulted | Consumer state must be current when predicate runs — event-before-predicate ordering eliminates race | ✓ Good |
| WH_KEYBOARD_LL owned by WindowDragHandler (not HookManager) | Keyboard hook is drag-specific; separating concerns avoids polluting HookManager with drag state | ✓ Good |
| Absolute-delta SetWindowPos: `newX = _windowOrigin.Left + (pt.X - _dragStartCursor.X)` | Eliminates accumulated drift from per-frame delta approach | ✓ Good |
| SWP_ASYNCWINDOWPOS in SetWindowPos flags | Prevents blocking hook callback if target app's message pump is slow | ✓ Good |
| LLKHF_INJECTED guard on VK_LCONTROL | AltGr sends a fake VK_LCONTROL before VK_RMENU — injected bit distinguishes it cleanly | ✓ Good |
| _suppressNextClick cleared on WM_LBUTTONUP (not WM_LBUTTONDOWN) | Full button pair (down+up) must be suppressed; clearing on down left orphaned LBUTTONUP reaching target | ✓ Good |
| Maximized window path: no _suppressNextClick | DRAG-06 requires click pass-through on maximized windows — setting suppression would break that | ✓ Good |
| GetKeyState self-heal on each BeginDragAttempt | SAS (Ctrl+Alt+Del) swallows key-up events; physical key state check self-heals stuck modifier state | ✓ Good |
| IsElevatedProcess check before committing drag state | UIPI blocks SetWindowPos cross-elevation; detecting upfront lets click pass through cleanly | ✓ Good |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-05-05 — v1.4 milestone complete*
