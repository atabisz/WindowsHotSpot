# Roadmap: WindowsHotSpot

## Milestones

- ✅ **v1.1 Single-Instance Guard** — Phase 1 (shipped 2026-03-17)
- ✅ **v1.2 Per-Corner Actions & Multi-Monitor** — Phases 2-4 (shipped 2026-03-18)
- ✅ **v1.4 Window Drag Anywhere** — Phases 5-7 (shipped 2026-05-05)
- **v1.5 Window Interactions** — Phases 8-10 (in progress)

## Phases

<details>
<summary>✅ v1.1 Single-Instance Guard (Phase 1) — SHIPPED 2026-03-17</summary>

- [x] Phase 1: Single-Instance Guard (2/2 plans) — completed 2026-03-17

See: `.planning/milestones/v1.1-ROADMAP.md`

</details>

<details>
<summary>✅ v1.2 Per-Corner Actions & Multi-Monitor (Phases 2-4) — SHIPPED 2026-03-18</summary>

- [x] Phase 2: Config Foundation (4/4 plans) — completed 2026-03-17
- [x] Phase 3: Detection Pipeline & Multi-Monitor (3/3 plans) — completed 2026-03-17
- [x] Phase 4: Custom Shortcut & Settings UI (4/4 plans) — completed 2026-03-18

See: `.planning/milestones/v1.2-ROADMAP.md`

</details>

<details>
<summary>✅ v1.4 Window Drag Anywhere (Phases 5-7) — SHIPPED 2026-05-05</summary>

- [x] Phase 5: Hook Suppression Infrastructure (1/1 plans) — completed 2026-05-05
- [x] Phase 6: WindowDragHandler (4/4 plans) — completed 2026-05-05
- [x] Phase 7: Wiring (1/1 plans) — completed 2026-05-05

See: `.planning/milestones/v1.4-ROADMAP.md`

</details>

### v1.5 Window Interactions

- [ ] **Phase 8: Hook Infrastructure** - Add WM_MOUSEWHEEL and WM_LBUTTONDBLCLK to HookManager
- [x] **Phase 9: Scroll Resize** - Implement Ctrl+Alt scroll-to-resize with configurable step size
- [ ] **Phase 10: Always-on-Top Toggle** - Implement Ctrl+Alt double-click AOT toggle with tray feedback

---

## Phase Details

### Phase 8: Hook Infrastructure
**Goal**: HookManager delivers wheel and double-click events to consumers
**Depends on**: Nothing (extends existing hook infrastructure)
**Requirements**: HOOK-03, HOOK-04
**Success Criteria** (what must be TRUE):
  1. Scrolling the mouse wheel while Ctrl+Alt is held fires a `MouseWheeled` event carrying the delta value and cursor position
  2. Double-clicking the left mouse button fires an event through HookManager (via `MouseButtonChanged` or a dedicated event)
  3. Neither event fires when Ctrl+Alt is not held (consumers gate on modifier state, but the hook itself dispatches unconditionally — delivery is observable at the consumer boundary)
  4. Existing drag behavior is unaffected — no regression in WM_MOUSEMOVE, WM_LBUTTONDOWN/UP, WM_RBUTTONDOWN/UP handling
**Plans**: TBD

### Phase 9: Scroll Resize
**Goal**: Users can resize any eligible window by holding Ctrl+Alt and scrolling
**Depends on**: Phase 8
**Requirements**: RESIZE-01, RESIZE-02, RESIZE-03, RESIZE-04, RESIZE-05, RESIZE-06, RESIZE-07
**Success Criteria** (what must be TRUE):
  1. Holding Ctrl+Alt and scrolling up over a window grows it symmetrically around the cursor; scrolling down shrinks it
  2. The Settings dialog exposes a "Scroll resize step" field (default 20px) and the value persists across app restarts
  3. Scrolling over a maximized window or an elevated (admin) window does nothing — no resize, no error
  4. AltGr (RightCtrl+LeftAlt) does not trigger resize — only physical LCtrl+LAlt activates it
**Plans**: 2 plans
Plans:
- [x] 09-01-PLAN.md — NativeMethods constants + AppSettings.ScrollResizeStep + ScrollResizeHandler class
- [x] 09-02-PLAN.md — SettingsForm Window Interactions group + HotSpotApplicationContext wiring
**UI hint**: yes

### Phase 10: Always-on-Top Toggle
**Goal**: Users can pin any eligible window to the top of the Z-order with a double-click gesture
**Depends on**: Phase 8
**Requirements**: AOT-01, AOT-02, AOT-03, AOT-04
**Success Criteria** (what must be TRUE):
  1. Holding Ctrl+Alt and double-clicking a window toggles its always-on-top state — the window stays above all others when pinned
  2. A tray balloon tooltip appears briefly after each toggle confirming "WindowsHotSpot: Pinned on top" or "WindowsHotSpot: Unpinned"
  3. Double-clicking an elevated (admin) window does nothing — no toggle, no tooltip, no error
  4. AltGr (RightCtrl+LeftAlt) does not trigger the toggle — only physical LCtrl+LAlt activates it
**Plans**: TBD

---

## Progress Table

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Single-Instance Guard | v1.1 | 2/2 | Complete | 2026-03-17 |
| 2. Config Foundation | v1.2 | 4/4 | Complete | 2026-03-17 |
| 3. Detection Pipeline & Multi-Monitor | v1.2 | 3/3 | Complete | 2026-03-17 |
| 4. Custom Shortcut & Settings UI | v1.2 | 4/4 | Complete | 2026-03-18 |
| 5. Hook Suppression Infrastructure | v1.4 | 1/1 | Complete | 2026-05-05 |
| 6. WindowDragHandler | v1.4 | 4/4 | Complete | 2026-05-05 |
| 7. Wiring | v1.4 | 1/1 | Complete | 2026-05-05 |
| 8. Hook Infrastructure | v1.5 | 0/? | Not started | - |
| 9. Scroll Resize | v1.5 | 2/2 | Complete | 2026-05-05 |
| 10. Always-on-Top Toggle | v1.5 | 0/? | Not started | - |

---

*Last updated: 2026-05-05 — Phase 9 complete (2/2 plans)*
