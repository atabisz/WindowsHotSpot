# Roadmap: WindowsHotSpot

## Overview

WindowsHotSpot delivers macOS-style hot corner functionality to Windows. The roadmap moves from the riskiest work (P/Invoke mouse hook, corner detection, Task View triggering) through configuration UI, to final packaging. Phase 1 proves the core value proposition with hardcoded settings; Phase 2 makes it user-configurable; Phase 3 makes it distributable.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: Core Detection and System Tray** - Global mouse hook detects corner dwell and triggers Task View; app lives in system tray
- [ ] **Phase 2: Settings and Configuration** - User can configure corner, zone size, dwell delay, and startup behavior via settings dialog
- [ ] **Phase 3: Distribution** - Self-contained executable packaged in an installer for end-user deployment

## Phase Details

### Phase 1: Core Detection and System Tray
**Goal**: User can move their mouse to a screen corner and reliably trigger Task View, with the app running silently in the system tray
**Depends on**: Nothing (first phase)
**Requirements**: CORE-01, CORE-02, CORE-03, CORE-04, CORE-05, CORE-06, TRAY-01, TRAY-02, TRAY-03, TRAY-04, TRAY-05, TRAY-06
**Success Criteria** (what must be TRUE):
  1. Moving the mouse to the top-left corner of any connected screen and holding it there for ~300ms opens Task View
  2. Dragging a window into the corner does not trigger Task View (drag suppression works)
  3. After Task View triggers, it does not trigger again until the mouse leaves and re-enters the corner zone
  4. The app has no taskbar button and shows only a system tray icon with working Settings, About, and Quit menu items
  5. Closing the app (via Quit or process termination) fully unregisters the mouse hook with no lingering system impact
**Plans**: 2 plans

Plans:
- [ ] 01-01-PLAN.md — Project scaffolding, P/Invoke layer, and core detection engine (HookManager, CornerDetector, ActionTrigger)
- [ ] 01-02-PLAN.md — System tray integration (ApplicationContext, tray icon, menu) and end-to-end verification

### Phase 2: Settings and Configuration
**Goal**: User can customize all hot corner behavior through a settings dialog, with preferences persisted across restarts
**Depends on**: Phase 1
**Requirements**: CONF-01, CONF-02, CONF-03, CONF-04, CONF-05, SETT-01, SETT-02, SETT-03, SETT-04, SETT-05
**Success Criteria** (what must be TRUE):
  1. User can change the active corner to any of the four screen corners and the change takes effect immediately without restarting the app
  2. User can adjust zone size and dwell delay, and those values persist across app restarts via the JSON config file
  3. User can toggle "Start with Windows" and the app correctly appears or disappears from Windows startup
  4. All settings changes apply immediately on save -- no app restart required
**Plans**: TBD

Plans:
- [ ] 02-01: TBD

### Phase 3: Distribution
**Goal**: End users can install WindowsHotSpot from a single setup.exe without needing .NET installed or admin privileges
**Depends on**: Phase 2
**Requirements**: DIST-01, DIST-02
**Success Criteria** (what must be TRUE):
  1. The published executable runs on a clean Windows 10/11 machine without requiring a separate .NET runtime installation
  2. The Inno Setup installer produces a single setup.exe that installs and runs the app without requesting admin elevation
**Plans**: TBD

Plans:
- [ ] 03-01: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 1 -> 2 -> 3

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Core Detection and System Tray | 0/2 | Planned | - |
| 2. Settings and Configuration | 0/0 | Not started | - |
| 3. Distribution | 0/0 | Not started | - |
