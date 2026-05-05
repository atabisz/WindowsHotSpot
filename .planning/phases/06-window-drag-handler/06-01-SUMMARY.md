---
plan: 06-01
phase: 06-window-drag-handler
status: complete
completed: 2026-05-05
---

# Plan 06-01 Summary: NativeMethods P/Invoke Declarations

## What Was Built

Added all Win32 P/Invoke declarations, structs, and constants required by WindowDragHandler to `WindowsHotSpot/Native/NativeMethods.cs`.

## Tasks Completed

| Task | Status | Commit |
|------|--------|--------|
| 06-01-01: Add P/Invoke methods, structs, constants | ✅ complete | deb57f3 |

## Key Files

### Modified
- `WindowsHotSpot/Native/NativeMethods.cs` — added all drag-related Win32 declarations

## What Was Added

**Constants:**
- `VK_LCONTROL` (0xA2), `VK_RCONTROL` (0xA3), `VK_LMENU` (0xA4), `VK_RMENU` (0xA5) — modifier key VK codes
- `LLKHF_INJECTED` (0x10) — AltGr fake LCtrl guard (GUARD-01)
- `GA_ROOT` (2) — GetAncestor flag
- `SWP_NOSIZE`, `SWP_NOZORDER`, `SWP_NOACTIVATE`, `SWP_ASYNCWINDOWPOS` (0x4000) — SetWindowPos flags (async prevents hook-callback blocking)
- `SW_SHOWMAXIMIZED` (3) — maximized window guard (DRAG-06)
- `IDC_ARROW`, `IDC_SIZEALL` — system cursor handles

**Structs:**
- `RECT` — Left/Top/Right/Bottom for GetWindowRect
- `WINDOWPLACEMENT` — includes `length` field (must pre-set to Marshal.SizeOf before calling GetWindowPlacement)

**P/Invoke methods:**
- `WindowFromPoint(POINT)` — find window under cursor
- `GetAncestor(IntPtr hwnd, uint gaFlags)` — walk to root window
- `SetWindowPos(...)` — move window non-blockingly
- `GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT)` — check maximized state
- `GetWindowRect(IntPtr hWnd, out RECT)` — get window bounds for origin calculation
- `LoadCursor(IntPtr hInstance, IntPtr lpCursorName)` — load system cursor
- `SetCursor(IntPtr hCursor)` — change cursor shape

## Self-Check: PASSED

- ✅ All 7 P/Invoke methods declared
- ✅ RECT and WINDOWPLACEMENT structs with correct layout
- ✅ VK_LCONTROL/VK_LMENU/VK_RMENU constants present
- ✅ LLKHF_INJECTED constant present for AltGr guard
- ✅ SWP_ASYNCWINDOWPOS present alongside other SWP flags
- ✅ IDC_SIZEALL and IDC_ARROW declared as static readonly IntPtr
- ✅ Build compiles with zero errors (verified by agent before stalling)
