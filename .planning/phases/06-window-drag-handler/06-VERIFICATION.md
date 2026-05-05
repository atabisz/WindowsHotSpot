---
phase: 06-window-drag-handler
verified: 2026-05-05T00:00:00Z
status: human_needed
score: 7/7 must-haves verified
overrides_applied: 0
deferred:
  - truth: "SelectedWindowDragPassThrough value is persisted to settings.json when the user clicks Save"
    addressed_in: "Phase 7"
    evidence: "Phase 7 goal: 'WindowDragHandler is a first-class component of HotSpotApplicationContext — created, connected, and disposed alongside CornerRouter'. Plan 06-02 objective explicitly states: 'Phase 7 (wiring) will connect SelectedWindowDragPassThrough to ConfigManager on Save — that step is explicitly out of scope here.'"
human_verification:
  - test: "LCtrl+LAlt + left drag on a restored window moves the window in real time"
    expected: "Window tracks the cursor delta smoothly; cursor changes to IDC_SIZEALL during drag"
    why_human: "Real-time window movement and cursor change cannot be verified programmatically without running the application"
  - test: "Drag started from a child control (e.g. Notepad text area) moves the root window, not just the child"
    expected: "Entire Notepad window moves, not internal text cursor"
    why_human: "GetAncestor walk-to-root behavior requires live window hierarchy; not verifiable statically"
  - test: "LCtrl+LAlt click does not deliver the click to the target app while drag is active"
    expected: "No text cursor insertion in Notepad; target sees no WM_LBUTTONDOWN"
    why_human: "Click suppression observable only by inspecting target app state at runtime"
  - test: "Clicking on a maximized window with LCtrl+LAlt does not start drag; click passes through"
    expected: "Maximized Notepad receives the click normally; no drag starts"
    why_human: "Maximized guard depends on GetWindowPlacement result which requires a live window handle"
  - test: "AltGr (Right Alt) does not trigger drag"
    expected: "Pressing AltGr and dragging a window does not move it"
    why_human: "LLKHF_INJECTED filtering is a runtime hook behavior; depends on OS injecting fake VK_LCONTROL which cannot be simulated statically"
  - test: "Releasing Ctrl or Alt mid-drag cancels drag at current position"
    expected: "Window stops tracking cursor and stays where it was when modifier released; cursor reverts to arrow"
    why_human: "CancelDragIfActive behavior requires live drag state"
  - test: "Settings form shows 'Window Dragging' GroupBox below 'System' with unchecked checkbox by default"
    expected: "GroupBox visible, checkbox labeled 'Pass through clicks when no window is draggable', unchecked"
    why_human: "Visual layout and WinForms rendering require running the application to confirm"
---

# Phase 6: WindowDragHandler Verification Report

**Phase Goal:** Users can drag any window by holding LCtrl+LAlt and left-click-dragging anywhere on the window body (not just the title bar). The feature guards against: maximized windows (no drag), AltGr false-triggers (LLKHF_INJECTED check), and modifier release mid-drag (cancels at current position). A settings checkbox controls whether clicks on non-draggable surfaces are swallowed or passed through.
**Verified:** 2026-05-05T00:00:00Z
**Status:** human_needed
**Re-verification:** No — initial verification

---

## Goal Achievement

### Observable Truths (from ROADMAP.md Success Criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| SC-1 | LCtrl+LAlt + left button on restored window begins drag — window moves in real time | ? HUMAN | `BeginDragAttempt()` sets `_isDragging=true`; `OnMouseMoved` calls `SetWindowPos` with absolute-delta math. Runtime behavior unverifiable statically. |
| SC-2 | Topmost window under cursor selected (not necessarily foreground) | ? HUMAN | `WindowFromPoint(pt)` + `GetAncestor(rawHwnd, GA_ROOT)` in `BeginDragAttempt`. Code path verified; actual topmost-window selection requires live OS test. |
| SC-3 | Initiating click not delivered to target app while drag active | ? HUMAN | `ShouldSuppress` returns `_isDragging` for all left-button messages. Click suppression logic verified in code; observable effect requires runtime test. |
| SC-4 | Releasing left button ends drag cleanly at final position | ? HUMAN | `OnMouseButtonChanged(false)` → `EndDrag()` sets `_isDragging=false`, calls `SetCursor(_arrowCursor)`. Does not reset window position. Logic correct; runtime behavior unverifiable. |
| SC-5 | LCtrl+LAlt click on maximized window does NOT start drag — click passes through | ? HUMAN | `GetWindowPlacement` guard: `if (wp.showCmd == NativeMethods.SW_SHOWMAXIMIZED) return;` (no `_suppressNextClick` set). Logic confirmed in code; requires live window test. |
| SC-6 | AltGr (RCtrl+LAlt) does not trigger drag | ? HUMAN | `KeyboardCallback` skips `_lCtrlDown=true` when `isInjected` is set (LLKHF_INJECTED check). `VK_RMENU` is NOT a tracked case. Code verified; runtime AltGr injection behavior needs human. |
| SC-7 | Ctrl or Alt released during drag cancels drag; window stays at current position | ? HUMAN | `CancelDragIfActive()` called on `VK_LCONTROL` key-up and `VK_LMENU` key-up. No position reset. Code path verified; requires runtime observation. |

**Score:** 7/7 truths have correct code implementations. All require human testing to confirm runtime behavior (standard for Win32 hook and UI code). Automated checks and build verification confirm the code logic is complete and correct.

**Note on human approval:** Plan 06-04 includes a `checkpoint:human-verify` task. The 06-04-SUMMARY.md records that all 8 manual test cases passed and the user typed "approved" on 2026-05-05. These human_verification items are listed here for formal completeness per verification process requirements.

---

### Deferred Items

Items not yet met but explicitly addressed in later milestone phases.

| # | Item | Addressed In | Evidence |
|---|------|-------------|----------|
| 1 | SelectedWindowDragPassThrough value persisted to settings.json on Save | Phase 7 | Plan 06-02 objective: "Phase 7 (wiring) will connect SelectedWindowDragPassThrough to ConfigManager on Save — that step is explicitly out of scope here." Phase 7 goal covers full HotSpotApplicationContext wiring. |

---

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `WindowsHotSpot/Native/NativeMethods.cs` | All 7 P/Invoke methods + RECT + WINDOWPLACEMENT + constants | VERIFIED | All 7 methods present: WindowFromPoint, GetAncestor, SetWindowPos, GetWindowPlacement, GetWindowRect, LoadCursor, SetCursor. RECT (line 98) and WINDOWPLACEMENT (line 107) declared with StructLayout(Sequential). All constants verified with correct hex values. |
| `WindowsHotSpot/Core/WindowDragHandler.cs` | Complete IDisposable drag component | VERIFIED | 224 lines. `class WindowDragHandler : IDisposable`. All public methods present: Install, OnMouseButtonChanged, OnMouseMoved, ShouldSuppress, Dispose. |
| `WindowsHotSpot/Config/AppSettings.cs` | WindowDragPassThrough bool property | VERIFIED | Line 37: `public bool WindowDragPassThrough { get; set; } = false;` after SameOnAllMonitors, before MonitorConfigs. |
| `WindowsHotSpot/UI/SettingsForm.cs` | Window Dragging GroupBox + SelectedWindowDragPassThrough | VERIFIED | Lines 46, 61, 290-308, 311, 353: all required elements present. GroupBox wired into Controls.AddRange. buttonPanelTop uses windowDragGroupTop+56. |
| `WindowsHotSpot/HotSpotApplicationContext.cs` | Temporary wiring for smoke test | VERIFIED | Lines 23, 79-83, 187-193: field, constructor wiring, and dispose all present. Three TODO(Phase 7) markers correct. |

---

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `WindowDragHandler.cs` | `NativeMethods.cs` | `NativeMethods.WindowFromPoint`, `GetAncestor`, `SetWindowPos`, `GetWindowPlacement`, `GetWindowRect`, `LoadCursor`, `SetCursor`, `VK_LCONTROL`, `VK_LMENU`, `LLKHF_INJECTED`, `SWP_ASYNCWINDOWPOS`, `SW_SHOWMAXIMIZED`, `IDC_SIZEALL`, `IDC_ARROW`, `GA_ROOT` | WIRED | All NativeMethods references confirmed in WindowDragHandler.cs. Constants used with correct values. |
| `WindowDragHandler.cs (ShouldSuppress)` | `HookManager.cs (SuppressionPredicate)` | `hookManager.SuppressionPredicate = handler.ShouldSuppress` | WIRED (temp) | Line 82 in HotSpotApplicationContext.cs. Marked TODO(Phase 7) — functional smoke-test wiring confirmed. |
| `WindowDragHandler.cs` | `AppSettings.cs` | `_settings.WindowDragPassThrough` read in `BeginDragAttempt` | WIRED | Lines 116, 124, 133 in WindowDragHandler.cs — three call sites all check `_settings.WindowDragPassThrough` before setting `_suppressNextClick`. |
| `SettingsForm.cs` | `AppSettings.cs` | `settings.WindowDragPassThrough` in constructor init | WIRED | Line 305: `Checked = settings.WindowDragPassThrough`. |
| `HotSpotApplicationContext.cs` | `WindowDragHandler.cs` | `MouseMoved += handler.OnMouseMoved`, `MouseButtonChanged += handler.OnMouseButtonChanged` | WIRED (temp) | Lines 80-83, 189-193 in HotSpotApplicationContext. Subscribe and unsubscribe both present. |

---

### Data-Flow Trace (Level 4)

| Artifact | Data Variable | Source | Produces Real Data | Status |
|----------|---------------|--------|--------------------|--------|
| `WindowDragHandler.cs` | `_lCtrlDown`, `_lAltDown` | `KeyboardCallback` via WH_KEYBOARD_LL hook | Yes — live kernel keyboard events | FLOWING |
| `WindowDragHandler.cs` | `_dragTarget`, `_windowOrigin` | `WindowFromPoint(pt)` + `GetWindowRect` | Yes — live Win32 window handles | FLOWING |
| `WindowDragHandler.cs` | `_settings.WindowDragPassThrough` | `AppSettings` field (JSON-deserialized from settings.json) | Yes — real settings value | FLOWING |
| `SettingsForm.cs` | `_windowDragPassThroughCheckBox.Checked` | `settings.WindowDragPassThrough` in constructor | Yes — initialized from live AppSettings | FLOWING |

---

### Behavioral Spot-Checks

Build verification is the only automated spot-check possible for this Win32 hook application.

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Project compiles with 0 errors, 0 warnings | `dotnet build WindowsHotSpot/WindowsHotSpot.csproj` | `Build succeeded. 0 Warning(s) 0 Error(s)` | PASS |
| WindowDragHandler.cs has all required public methods | grep Install, OnMouseButtonChanged, OnMouseMoved, ShouldSuppress | All 4 present | PASS |
| VK_RMENU not a tracked case in KeyboardCallback | grep in WindowDragHandler.cs | Comment only — not a tracked switch case | PASS |
| LLKHF_INJECTED guard on VK_LCONTROL key-down | grep in WindowDragHandler.cs line 191 | `if (isKeyDown && !isInjected) _lCtrlDown = true` | PASS |
| Maximized guard present in BeginDragAttempt | grep SW_SHOWMAXIMIZED | `if (wp.showCmd == NativeMethods.SW_SHOWMAXIMIZED) return;` | PASS |
| Absolute-delta math in OnMouseMoved | grep _windowOrigin | `newX = _windowOrigin.Left + (pt.X - _dragStartCursor.X)` | PASS |
| WINDOWPLACEMENT.length set before GetWindowPlacement | grep Marshal.SizeOf | `wp.length = (uint)Marshal.SizeOf<NativeMethods.WINDOWPLACEMENT>()` | PASS |
| CancelDragIfActive called on both modifier key-up | grep CancelDragIfActive | 3 occurrences: definition + VK_LCONTROL key-up + VK_LMENU key-up | PASS |
| _suppressNextClick NOT set on maximized path | inspect BeginDragAttempt | `SW_SHOWMAXIMIZED` branch returns without setting `_suppressNextClick` | PASS |

---

### Requirements Coverage

| Requirement | Source Plan(s) | Description | Status | Evidence |
|-------------|----------------|-------------|--------|----------|
| DRAG-01 | 06-02, 06-03 | User can hold Ctrl+Alt and left-click-drag anywhere to move window | SATISFIED | `BeginDragAttempt` requires `_lCtrlDown && _lAltDown`; wired to `OnMouseButtonChanged(true)` |
| DRAG-02 | 06-01, 06-03 | Topmost window under cursor moved | SATISFIED | `WindowFromPoint(pt)` + `GetAncestor(rawHwnd, GA_ROOT)` resolves root window from child control |
| DRAG-03 | 06-03 | Initiating click not forwarded to target app | SATISFIED | `ShouldSuppress` returns `_isDragging` for WM_LBUTTONDOWN and WM_LBUTTONUP |
| DRAG-04 | 06-01, 06-03 | Window position tracks cursor delta in real time | SATISFIED | `OnMouseMoved` uses absolute-delta math + `SWP_ASYNCWINDOWPOS` |
| DRAG-05 | 06-03 | Drag ends cleanly on mouse button release | SATISFIED | `OnMouseButtonChanged(false)` → `EndDrag()` clears `_isDragging`, `_dragTarget`, restores cursor |
| DRAG-06 | 06-01, 06-03 | Maximized windows ignored — click passes through | SATISFIED | `GetWindowPlacement` guard with `SW_SHOWMAXIMIZED` check; returns without setting `_suppressNextClick` |
| GUARD-01 | 06-01, 06-03 | Only LCtrl+LAlt triggers drag (not AltGr) | SATISFIED | `isInjected = (kb.flags & LLKHF_INJECTED) != 0`; injected VK_LCONTROL skips `_lCtrlDown=true`; VK_RMENU not tracked |
| GUARD-02 | 06-03 | Modifier release mid-drag cancels drag | SATISFIED | `CancelDragIfActive()` called on `VK_LCONTROL` key-up and `VK_LMENU` key-up |
| WIRE-01 | Phase 7 | WindowDragHandler wired to HookManager in HotSpotApplicationContext | DEFERRED | Explicitly assigned to Phase 7. Temporary wiring in HotSpotApplicationContext is smoke-test scaffolding only (TODO Phase 7 markers). |
| WIRE-02 | Phase 7 | WindowDragHandler disposed cleanly | DEFERRED | Explicitly assigned to Phase 7. Dispose called in DisposeComponents() but temporary wiring pathway, not final. |

---

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `HotSpotApplicationContext.cs` | 23, 78, 186 | `TODO(Phase 7): move to permanent wiring` | Info | Intentional scaffolding markers for temporary smoke-test wiring — not implementation stubs. Phase 7 will replace this with permanent wiring. No behavioral impact: wiring is fully functional. |

No stubs, no placeholder returns, no empty implementations found in Phase 6 production code.

---

### Human Verification Required

The following manual tests are required. Per 06-04-SUMMARY.md, these were executed on 2026-05-05 and the user approved all 8 test cases. This section is retained per verification process requirements for formal completeness.

#### 1. Basic Drag (DRAG-01, DRAG-04)

**Test:** Open Notepad, restore it, hold LCtrl+LAlt, click and drag anywhere on the window body.
**Expected:** Window moves in real time; cursor changes to four-arrow IDC_SIZEALL; window stays at final position on release.
**Why human:** Real-time SetWindowPos behavior requires running application.

#### 2. Child Control Drag (DRAG-02)

**Test:** Drag from inside Notepad's text area (not title bar) while holding LCtrl+LAlt.
**Expected:** The entire Notepad window moves — not just the internal text cursor.
**Why human:** GetAncestor root-walk behavior requires live window hierarchy.

#### 3. Click Suppression (DRAG-03)

**Test:** Hold LCtrl+LAlt, click (no drag) on Notepad text area at a specific location.
**Expected:** No text cursor insertion in Notepad — click was suppressed.
**Why human:** Click suppression is visible only via target app state.

#### 4. Maximized Window Guard (DRAG-06)

**Test:** Maximize Notepad, hold LCtrl+LAlt, click anywhere on the maximized window.
**Expected:** No drag starts; click passes through to Notepad normally.
**Why human:** GetWindowPlacement result depends on live window handle.

#### 5. AltGr Guard (GUARD-01)

**Test:** Press Right Alt (AltGr) only, drag on any normal window.
**Expected:** No drag starts — RCtrl+LAlt combination does not activate drag.
**Why human:** LLKHF_INJECTED OS injection behavior is runtime-only.

#### 6. Modifier Release Cancels Drag (GUARD-02)

**Test:** Start dragging, release LCtrl while still holding LAlt.
**Expected:** Drag cancelled; window stays at current mid-drag position; cursor reverts to arrow.
**Why human:** CancelDragIfActive behavior requires active drag state.

#### 7. Settings Form UI

**Test:** Right-click tray icon → Settings, scroll to bottom.
**Expected:** "Window Dragging" GroupBox visible below "System", containing "Pass through clicks when no window is draggable" checkbox (unchecked by default).
**Why human:** WinForms rendering and visual layout require running application.

---

### Gaps Summary

No gaps found. All 7 roadmap success criteria have correct code implementations. All 8 phase requirements (DRAG-01 through DRAG-06, GUARD-01, GUARD-02) are implemented in the codebase and verified by grep-level analysis.

The `human_needed` status reflects that this is a Win32 hook-based UI feature — the core behaviors (window movement, click suppression, cursor change, AltGr rejection) are not programmatically testable without running the application. According to 06-04-SUMMARY.md, all manual tests were executed and approved by the user on 2026-05-05.

WIRE-01 and WIRE-02 are correctly deferred to Phase 7 per the ROADMAP.md traceability table.

---

_Verified: 2026-05-05T00:00:00Z_
_Verifier: Claude (gsd-verifier)_
