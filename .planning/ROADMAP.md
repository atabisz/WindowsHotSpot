# Roadmap: WindowsHotSpot

## Milestones

- ✅ **v1.1 Single-Instance Guard** — Phase 1 (shipped 2026-03-17)
- 🚧 **v1.2 Per-Corner Actions & Multi-Monitor** — Phases 2-4 (in progress)

## Phases

<details>
<summary>✅ v1.1 Single-Instance Guard (Phase 1) — SHIPPED 2026-03-17</summary>

- [x] Phase 1: Single-Instance Guard (2/2 plans) — completed 2026-03-17

</details>

### 🚧 v1.2 Per-Corner Actions & Multi-Monitor (In Progress)

**Milestone Goal:** Each corner on each monitor can independently trigger a different action (Win+Tab, Show Desktop, Action Center, custom shortcut, or disabled).

- [x] **Phase 2: Config Foundation** - New AppSettings schema, CornerAction enum, per-corner data model, ActionDispatcher, and v1.x migration
- [x] **Phase 3: Detection Pipeline & Multi-Monitor** - CornerRouter (one detector per active corner per monitor), WM_DISPLAYCHANGE handling, and live settings rebuild (completed 2026-03-17)
- [ ] **Phase 4: Custom Shortcut & Settings UI** - Hotkey recorder, 2×2 corner grid settings form with monitor selector

## Phase Details

### Phase 2: Config Foundation
**Goal**: The data model for per-corner actions is stable and existing user settings survive the upgrade
**Depends on**: Phase 1
**Requirements**: CONF-01, CRNA-01, CRNA-02, CRNA-03, CRNA-04, CRNA-06
**Success Criteria** (what must be TRUE):
  1. Launching the new build on a machine with a v1.x settings.json preserves the previously configured corner and dwell delay — no settings are silently reset
  2. Each of the four corners can be saved and reloaded with an independently assigned action (Win+Tab, Show Desktop, Action Center, or Disabled)
  3. A corner saved as Disabled fires no action when the mouse dwells there
  4. ActionDispatcher routes Win+Tab, Show Desktop (Win+D), and Action Center (Win+A) to the correct SendInput call
**Plans**: 4 plans

Plans:
- [x] 02-01-PLAN.md — AppSettings v2 schema: CornerAction enum + CornerActions dict + LegacyCorner migration hook
- [x] 02-02-PLAN.md — ConfigManager: MigrateV1, SaveFile, fill-missing-keys
- [x] 02-03-PLAN.md — ActionDispatcher + NativeMethods VK_D/VK_A constants
- [x] 02-04-PLAN.md — Wiring: CornerDetector + HotSpotApplicationContext updated for v2

### Phase 3: Detection Pipeline & Multi-Monitor
**Goal**: Every active corner on every connected monitor detects dwell independently and fires the correct action
**Depends on**: Phase 2
**Requirements**: MMON-01, MMON-02, MMON-03, MMON-04
**Success Criteria** (what must be TRUE):
  1. On a two-monitor setup, dwelling in a corner on Monitor A fires Monitor A's configured action; dwelling in the matching corner on Monitor B fires Monitor B's independently configured action
  2. Plugging in a second monitor while the app is running activates its corners immediately — no restart required
  3. A newly connected monitor that has no saved config defaults to all corners disabled
  4. Unplugging a monitor and plugging it back in restores its previously saved corner configuration
**Plans**: 3 plans

Plans:
- [ ] 03-01-PLAN.md — AppSettings MonitorCornerConfig + CornerDetector screen-scoped refactor
- [ ] 03-02-PLAN.md — CornerRouter: per-monitor detector pool with Rebuild() and hot-path routing
- [ ] 03-03-PLAN.md — HotSpotApplicationContext wiring: CornerRouter + DisplaySettingsChanged subscription

### Phase 4: Custom Shortcut & Settings UI
**Goal**: Users can assign any recorded keystroke to a corner and configure all corners visually in a redesigned settings dialog
**Depends on**: Phase 3
**Requirements**: CRNA-05, UI-01, UI-02
**Success Criteria** (what must be TRUE):
  1. User can click a Record button for any corner, press a key combination, and have it saved as that corner's action — Escape cancels without saving
  2. The Settings dialog shows a 2×2 grid of corner controls that visually matches the physical screen corner layout
  3. When more than one monitor is connected, a monitor selector appears and switching between monitors shows each monitor's independent corner assignments
  4. Dwelling in a corner configured with a recorded custom shortcut sends that exact keystroke via SendInput
**Plans**: 4 plans

Plans:
- [ ] 04-01-PLAN.md — CustomShortcut data model + dispatch pipeline (AppSettings, ActionDispatcher, CornerDetector, CornerRouter)
- [ ] 04-02-PLAN.md — KeyRecorderPanel: focusable Panel subclass for click-to-record keystroke capture
- [ ] 04-03-PLAN.md — SettingsForm redesign (2×2 grid + monitor selector) + HotSpotApplicationContext wiring
- [ ] 04-04-PLAN.md — End-to-end verification checkpoint

## Progress

| Phase | Milestone | Plans Complete | Status | Completed |
|-------|-----------|----------------|--------|-----------|
| 1. Single-Instance Guard | v1.1 | 2/2 | Complete | 2026-03-17 |
| 2. Config Foundation | v1.2 | 4/4 | Complete | 2026-03-17 |
| 3. Detection Pipeline & Multi-Monitor | v1.2 | 3/3 | Complete | 2026-03-17 |
| 4. Custom Shortcut & Settings UI | 1/4 | In Progress|  | - |

---

*Last updated: 2026-03-18 — Phase 4 planned (4 plans); ready to execute*
