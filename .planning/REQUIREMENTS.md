# Requirements: WindowsHotSpot

**Defined:** 2026-03-17
**Core Value:** The mouse hot corner fires Task View reliably every time, on any screen, with zero friction.

## v1.2 Requirements

### Corner Actions

- [x] **CRNA-01**: Each corner can be independently configured with an action or set to disabled
- [x] **CRNA-02**: User can assign Win+Tab (Task View) to any corner
- [x] **CRNA-03**: User can assign Show Desktop (Win+D) to any corner
- [x] **CRNA-04**: User can assign Action Center (Win+A) to any corner
- [ ] **CRNA-05**: User can record a custom keystroke for any corner (click-to-record; Escape cancels)
- [x] **CRNA-06**: A corner set to disabled triggers no action when dwelled

### Multi-Monitor

- [ ] **MMON-01**: Each connected monitor has its own independent set of 4 corner configurations
- [ ] **MMON-02**: Adding or removing a monitor updates the active corner set without restart
- [ ] **MMON-03**: A new unrecognised monitor defaults to all-corners disabled
- [ ] **MMON-04**: Config for a disconnected monitor is silently retained for when it reconnects

### Config Migration

- [ ] **CONF-01**: Existing v1.x settings.json is migrated to the new schema without data loss

### Settings UI

- [ ] **UI-01**: Settings shows a 2×2 corner layout per monitor for visual corner assignment
- [ ] **UI-02**: Settings shows a monitor selector when more than one monitor is connected

## Future Requirements

### UX

- Per-corner dwell delay — deferred, multiplies settings surface without clear user value
- Launch application action — deferred, transforms tool into general launcher
- Per-app profiles — different product category

## Out of Scope

| Feature | Reason |
|---------|--------|
| Per-corner dwell delay | Multiplies settings surface 16x for an edge case; global delay sufficient |
| Launch application action | Transforms the tool into a general launcher — different product |
| Per-app profiles | Different product category |
| EDID monitor naming | Complex SetupAPI; "Display 1 (Primary)" label is sufficient |

## Traceability

| Requirement | Phase | Status |
|-------------|-------|--------|
| CONF-01 | Phase 2 | Pending |
| CRNA-01 | Phase 2 | Complete |
| CRNA-02 | Phase 2 | Complete |
| CRNA-03 | Phase 2 | Complete |
| CRNA-04 | Phase 2 | Complete |
| CRNA-06 | Phase 2 | Complete |
| MMON-01 | Phase 3 | Pending |
| MMON-02 | Phase 3 | Pending |
| MMON-03 | Phase 3 | Pending |
| MMON-04 | Phase 3 | Pending |
| CRNA-05 | Phase 4 | Pending |
| UI-01 | Phase 4 | Pending |
| UI-02 | Phase 4 | Pending |

**Coverage:**
- v1.2 requirements: 13 total
- Mapped to phases: 13
- Unmapped: 0 ✓

---
*Requirements defined: 2026-03-17*
*Last updated: 2026-03-17 — traceability updated after v1.2 roadmap creation*
