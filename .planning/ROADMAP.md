# Roadmap: WindowsHotSpot

## Milestones

- ✅ **v1.1 Single-Instance Guard** — Phase 1 (shipped 2026-03-17)
- ✅ **v1.2 Per-Corner Actions & Multi-Monitor** — Phases 2-4 (shipped 2026-03-18)
- 🔄 **v1.4 Window Drag Anywhere** — Phases 5-7 (in progress)

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

### v1.4 Window Drag Anywhere

- [ ] **Phase 5: Hook Suppression Infrastructure** — Add event suppression to HookManager
- [ ] **Phase 6: WindowDragHandler** — Core drag logic with modifier guard and maximized-window protection
- [ ] **Phase 7: Wiring** — Wire WindowDragHandler into HotSpotApplicationContext and dispose cleanly

---

## Phase Details

### Phase 5: Hook Suppression Infrastructure
**Goal**: HookManager can selectively suppress mouse button events so drag consumers can prevent clicks from reaching target windows
**Depends on**: Nothing (extends existing HookManager)
**Requirements**: HOOK-01, HOOK-02
**Success Criteria** (what must be TRUE):
  1. A consumer can register a suppression predicate with HookManager and, when the predicate returns true, WM_LBUTTONDOWN / WM_LBUTTONUP return 1 (consumed) instead of calling the next hook
  2. WM_MOUSEMOVE events are never suppressed regardless of any registered predicate — move events always call through
  3. When no suppression predicate is registered (or it returns false), hook behavior is identical to pre-Phase-5 call-through behavior
  4. The suppression API is usable by WindowDragHandler without modifying HookManager internals again
**Plans**: 1 plan

Plans:
- [~] 05-01-PLAN.md — Add SuppressionPredicate property and restructure HookCallback (Task 1 done, awaiting checkpoint verification)

### Phase 6: WindowDragHandler
**Goal**: Users can hold Ctrl+Alt and drag any non-maximized window to move it, with smooth real-time tracking, clean release, and AltGr protection
**Depends on**: Phase 5
**Requirements**: DRAG-01, DRAG-02, DRAG-03, DRAG-04, DRAG-05, DRAG-06, GUARD-01, GUARD-02
**Success Criteria** (what must be TRUE):
  1. Holding LCtrl+LAlt and pressing the left mouse button on any part of a restored window begins a drag — the window moves with the cursor delta in real time
  2. The topmost window under the cursor is selected for drag, not necessarily the foreground window
  3. The initiating click is not delivered to the target application while drag mode is active — the target app sees no click
  4. Releasing the left mouse button ends the drag cleanly, leaving the window at its final position
  5. Clicking on a maximized window while holding LCtrl+LAlt does not start a drag — the click passes through to the application normally
  6. Holding RCtrl+LAlt (AltGr) does not trigger drag — only the LCtrl+LAlt combination activates it
  7. If Ctrl or Alt is released during an active drag, the drag is cancelled and the window remains at its current (mid-drag) position
**Plans**: TBD
**UI hint**: yes

### Phase 7: Wiring
**Goal**: WindowDragHandler is a first-class component of HotSpotApplicationContext — created, connected, and disposed alongside CornerRouter with no resource leaks
**Depends on**: Phase 6
**Requirements**: WIRE-01, WIRE-02
**Success Criteria** (what must be TRUE):
  1. WindowDragHandler is instantiated in HotSpotApplicationContext and subscribed to HookManager events at application startup, alongside CornerRouter
  2. When the application exits (tray Quit or system shutdown), WindowDragHandler.Dispose() is called and all hook subscriptions and unmanaged resources are released without error
**Plans**: TBD

---

## Progress Table

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 5. Hook Suppression Infrastructure | 0/1 | In progress (checkpoint) | - |
| 6. WindowDragHandler | 0/? | Not started | - |
| 7. Wiring | 0/? | Not started | - |

---

*Last updated: 2026-05-05 — Phase 5 planned (1 plan)*
