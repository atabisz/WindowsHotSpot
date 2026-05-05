# Requirements: WindowsHotSpot v1.5 Window Interactions

## Milestone v1.5 Requirements

### Scroll Resize

- [ ] **RESIZE-01**: User can hold Ctrl+Alt and scroll the mouse wheel to resize the window under the cursor — scroll up grows, scroll down shrinks
- [ ] **RESIZE-02**: Resize is applied symmetrically to all four edges, anchored around the cursor position (window grows/shrinks toward and away from the cursor)
- [ ] **RESIZE-03**: Step size per scroll notch is configurable in Settings (default 20px); the setting persists across restarts
- [ ] **RESIZE-04**: Resize is silently clamped at Windows minimum window size — no error, no feedback
- [ ] **RESIZE-05**: Scroll resize is skipped for maximized windows (consistent with drag behavior)
- [ ] **RESIZE-06**: Scroll resize is skipped for elevated (admin) windows — UIPI constraint (consistent with drag behavior)
- [ ] **RESIZE-07**: AltGr (RCtrl+LAlt) does not trigger scroll resize — only LCtrl+LAlt activates it (consistent with GUARD-01)

### Always-on-Top Toggle

- [ ] **AOT-01**: User can Ctrl+Alt+double-click a window to toggle its always-on-top state
- [ ] **AOT-02**: A tray balloon tooltip briefly appears confirming the state change: "WindowsHotSpot: Pinned on top" or "WindowsHotSpot: Unpinned"
- [ ] **AOT-03**: Always-on-top toggle is skipped for elevated (admin) windows — UIPI constraint
- [ ] **AOT-04**: AltGr (RCtrl+LAlt) does not trigger the toggle — only LCtrl+LAlt activates it

### Hook Infrastructure

- [ ] **HOOK-03**: HookManager handles WM_MOUSEWHEEL events and fires a new `MouseWheeled` event with delta and cursor position
- [ ] **HOOK-04**: HookManager handles WM_LBUTTONDBLCLK events and fires via the existing `MouseButtonChanged` event or a new dedicated event

## Future Requirements

- Per-app exclusion list for resize/AOT (deferred — different product surface)
- Resize step configurable per-axis (width vs height independently) — deferred, over-engineering for v1.5
- Visual indicator on pinned windows (title bar tint or border) — deferred, requires accessibility tree work

## Out of Scope

- Resize by dragging window edges — different UX pattern (drag-resize vs scroll-resize); already in Out of Scope from v1.4
- Always-on-top for elevated windows — UIPI prevents `SetWindowPos` from non-elevated process; no workaround without elevation match
- Persistent always-on-top state across app restarts — storing per-HWND state is fragile (HWNDs are not stable across launches); intentionally ephemeral

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| HOOK-03 | Phase 8 | Pending |
| HOOK-04 | Phase 8 | Pending |
| RESIZE-01 | Phase 9 | Pending |
| RESIZE-02 | Phase 9 | Pending |
| RESIZE-03 | Phase 9 | Pending |
| RESIZE-04 | Phase 9 | Pending |
| RESIZE-05 | Phase 9 | Pending |
| RESIZE-06 | Phase 9 | Pending |
| RESIZE-07 | Phase 9 | Pending |
| AOT-01 | Phase 10 | Pending |
| AOT-02 | Phase 10 | Pending |
| AOT-03 | Phase 10 | Pending |
| AOT-04 | Phase 10 | Pending |
