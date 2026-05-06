# Roadmap: WindowsHotSpot

## Milestones

- ✅ **v1.1 Single-Instance Guard** — Phase 1 (shipped 2026-03-17)
- ✅ **v1.2 Per-Corner Actions & Multi-Monitor** — Phases 2-4 (shipped 2026-03-18)
- ✅ **v1.4 Window Drag Anywhere** — Phases 5-7 (shipped 2026-05-05)
- ✅ **v1.5 Window Interactions** — Phases 8-10 (shipped 2026-05-06)
- 🔄 **v1.6 Window Transparency** — Phases 11-12 (active)

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

<details>
<summary>✅ v1.5 Window Interactions (Phases 8-10) — SHIPPED 2026-05-06</summary>

- [x] Phase 8: Hook Infrastructure (1/1 plans) — completed 2026-05-06
- [x] Phase 9: Scroll Resize (2/2 plans) — completed 2026-05-06
- [x] Phase 10: Always-on-Top Toggle (2/2 plans) — completed 2026-05-06

See: `.planning/milestones/v1.5-ROADMAP.md`

</details>

### v1.6 Window Transparency (Phases 11-12)

- [x] **Phase 11: WindowTransparencyHandler** - Core handler: modifier tracking, window pipeline, WS_EX_LAYERED/LWA_ALPHA, alpha clamp
- [ ] **Phase 12: Wiring + Settings** - Wire handler into app context, add TransparencyStep to AppSettings and Settings UI

---

## Phase Details

### Phase 11: WindowTransparencyHandler
**Goal**: The transparency adjustment gesture is fully implemented and self-contained
**Depends on**: Phase 10 (HookManager with MouseWheeled and WheelSuppressionPredicate in place)
**Requirements**: TRNSP-01, TRNSP-02, TRNSP-03, TRNSP-05, GUARD-01, GUARD-02, GUARD-03
**Success Criteria** (what must be TRUE):
  1. Holding LCtrl+LAlt+LShift and scrolling over a window changes its transparency; AltGr does not trigger
  2. Scroll up increases opacity, scroll down decreases opacity; alpha is clamped to 25–255 with no value outside that range possible
  3. A window that already has WS_EX_LAYERED set (e.g. a color-key window) retains its existing flags after transparency adjustment
  4. Scrolling over a maximized window or an elevated (admin) window is ignored and the scroll event passes through normally
**Plans**: 1 plan
Plans:
- [x] 11-01-PLAN.md — NativeMethods additions + AppSettings.TransparencyStep + WindowTransparencyHandler implementation

### Phase 12: Wiring + Settings
**Goal**: WindowTransparencyHandler is active in the running application and its step size is user-configurable
**Depends on**: Phase 11
**Requirements**: TRNSP-04, WIRE-01, WIRE-02
**Success Criteria** (what must be TRUE):
  1. Transparency adjustment works in a running build — Ctrl+Alt+Shift+scroll changes window opacity without any manual wiring step
  2. Settings form "Window Interactions" section exposes a transparency step size field (default 10, range 1–50)
  3. Changing the step size in Settings and saving takes effect immediately for subsequent scroll gestures
  4. Closing the application disposes WindowTransparencyHandler cleanly with no hook leaks or exceptions on exit
**Plans**: TBD
**UI hint**: yes

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
| 8. Hook Infrastructure | v1.5 | 1/1 | Complete | 2026-05-06 |
| 9. Scroll Resize | v1.5 | 2/2 | Complete | 2026-05-06 |
| 10. Always-on-Top Toggle | v1.5 | 2/2 | Complete | 2026-05-06 |
| 11. WindowTransparencyHandler | v1.6 | 1/1 | Complete | 2026-05-06 |
| 12. Wiring + Settings | v1.6 | 0/? | Not started | - |

---

*Last updated: 2026-05-06 — Phase 11 complete (WindowTransparencyHandler)*
