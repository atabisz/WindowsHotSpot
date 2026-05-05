---
plan: 06-03
phase: 06-window-drag-handler
status: complete
completed: 2026-05-05
---

# Plan 06-03 Summary: WindowDragHandler.cs — Core Drag Logic

## What Was Built

Created `WindowsHotSpot/Core/WindowDragHandler.cs` — the complete, self-contained drag-anywhere component. It installs its own WH_KEYBOARD_LL hook, tracks LCtrl+LAlt state, moves windows in real time via absolute-delta SetWindowPos, and integrates with HookManager via three public methods.

## Tasks Completed

| Task | Status | Commit |
|------|--------|--------|
| 06-03-01: Create WindowDragHandler.cs with complete implementation | ✅ complete | 8f2fd95 |

## Key Files

### Created
- `WindowsHotSpot/Core/WindowDragHandler.cs` — 224 lines, sealed IDisposable

## What Was Built

**Fields:**
- `_kbHookId`, `_kbCallback` — keyboard hook handle + GC-pinned delegate
- `_lCtrlDown`, `_lAltDown` — modifier state
- `_isDragging`, `_suppressNextClick`, `_dragTarget`, `_dragStartCursor`, `_windowOrigin` — drag state
- `_sizeAllCursor`, `_arrowCursor` — static readonly system cursor handles (loaded once at class init)

**Public API (HookManager integration surface):**
- `Install()` — installs WH_KEYBOARD_LL, mirrors HookManager.Install() pattern
- `OnMouseButtonChanged(bool isDown)` — dispatches to BeginDragAttempt/EndDrag
- `OnMouseMoved(Point pt)` — moves window via absolute-delta SetWindowPos (hot path)
- `ShouldSuppress(int msg)` — registered as HookManager.SuppressionPredicate
- `Dispose()` — unhooks keyboard, clears all state

**Key implementation details:**

- **GUARD-01 (AltGr guard):** `isInjected = (kb.flags & LLKHF_INJECTED) != 0` — injected VK_LCONTROL events (from AltGr) skip `_lCtrlDown=true`
- **DRAG-06 (maximized guard):** `wp.length = Marshal.SizeOf<WINDOWPLACEMENT>()` set before GetWindowPlacement call; `wp.showCmd == SW_SHOWMAXIMIZED` causes return without drag or suppression
- **SWP_ASYNCWINDOWPOS:** included in SetWindowPos flags — prevents blocking hook callback on slow target app message pump
- **D-04 (_suppressNextClick):** set when `!WindowDragPassThrough` on any no-valid-target path (WindowFromPoint zero, GetAncestor zero, GetWindowPlacement fails); NOT set for maximized path (DRAG-06 requires pass-through)
- **GUARD-02:** CancelDragIfActive() called on VK_LCONTROL and VK_LMENU key-up — drag cancelled at current position
- **Absolute-delta math:** `newX = _windowOrigin.Left + (pt.X - _dragStartCursor.X)` — no drift

## Requirements Covered

DRAG-01, DRAG-02, DRAG-03, DRAG-04, DRAG-05, DRAG-06, GUARD-01, GUARD-02

## Self-Check: PASSED

- ✅ Build: 0 errors, 0 warnings
- ✅ `class WindowDragHandler : IDisposable` declared
- ✅ Install(), OnMouseButtonChanged(), OnMouseMoved(), ShouldSuppress(), Dispose() all present
- ✅ VK_LCONTROL tracked with LLKHF_INJECTED guard
- ✅ VK_RMENU not tracked (only in comment)
- ✅ SW_SHOWMAXIMIZED check in BeginDragAttempt
- ✅ SWP_ASYNCWINDOWPOS in OnMouseMoved SWP_FLAGS
- ✅ Marshal.SizeOf<WINDOWPLACEMENT>() sets wp.length before GetWindowPlacement
- ✅ _suppressNextClick: 8 references (field, set ×3 in BeginDragAttempt, read+clear in ShouldSuppress, clear in Dispose)
- ✅ CancelDragIfActive: 3 references (definition + VK_LCONTROL key-up + VK_LMENU key-up)
- ✅ WindowFromPoint + GetAncestor called in BeginDragAttempt
