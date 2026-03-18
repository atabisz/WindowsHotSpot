# Project Retrospective

*A living document updated after each milestone. Lessons feed forward into future planning.*

---

## Milestone: v1.2 — Per-Corner Actions & Multi-Monitor

**Shipped:** 2026-03-18
**Phases:** 3 (02–04) | **Plans:** 11 | **Timeline:** 7 days

### What Was Built
- Per-corner action model with `CornerAction` enum (Disabled/TaskView/ShowDesktop/ActionCenter/Custom) replacing the single global action
- Automatic v1.x → v1.2 settings migration using a nullable `[JsonPropertyName]` hook — transparent to users
- `CornerRouter` owning a per-(monitor, corner) `CornerDetector` pool, rebuilt on settings changes and `WM_DISPLAYCHANGE`
- Live monitor hot-plug: no restart needed when monitors are connected/disconnected
- `KeyRecorderPanel` with `WH_KEYBOARD_LL` for Win-key capture; bare alphanumerics rejected, modifier combos accepted
- Redesigned `SettingsForm`: 2×2 corner grid per monitor + monitor selector + Same-on-all-monitors toggle

### What Worked
- **Immutable detector pattern**: `CornerDetector` fields readonly, rebuilt rather than mutated — compiler-enforced contract made bugs impossible
- **Single rebuild path**: both `SettingsChanged` and `DisplaySettingsChanged` call `CornerRouter.Rebuild()` — no special-casing for topology vs config changes
- **Phased data model first**: building the config foundation (Phase 2) before the router (Phase 3) before the UI (Phase 4) meant each layer had a stable contract to build on
- **QA-driven additions in Phase 4**: end-to-end verification surfaced real UX gaps (Win-key recording, Same-on-all-monitors) that weren't in the original spec but were clearly needed

### What Was Inefficient
- **`WH_KEYBOARD_LL` for Win-key wasn't anticipated in Phase 4 planning** — discovered during verification, required adding a second low-level hook to an already hook-heavy app; could have been surfaced earlier in research
- **`Screen.DeviceName` stability is medium-confidence** — noted as a risk but not validated during the milestone; if drivers re-enumerate, monitor identity keys could shift silently

### Patterns Established
- **Immutable detector, mutable router**: `CornerDetector` is a value-like object rebuilt on change; `CornerRouter` is the stateful container
- **`[JsonPropertyName]` nullable migration hook**: zero-overhead schema migration that auto-heals on next save without a separate migration pass
- **`record struct` for named pairing**: `ScreenDetectors` uses record struct instead of tuple for readable named fields in hot-path foreach
- **Wire-then-install order**: `CornerRouter.Rebuild()` called before `HookManager.Install()` — pool populated before mouse events can arrive
- **Static event cleanup first**: `SystemEvents.DisplaySettingsChanged` unsubscribed before component disposal to prevent memory leak

### Key Lessons
1. **Capture Win-key limitations in Phase research** — Win key is absorbed by Windows before WinForms; any UI that needs to record Win combos requires `WH_KEYBOARD_LL`. Note this upfront.
2. **Verify monitor identity key stability early** — `Screen.DeviceName` as config key is the right call but needs empirical validation (driver reinstall, sleep/wake, RDP). Add a V&V step for this in the next monitor-touching feature.
3. **QA pass belongs in every phase, not just the last plan** — Phase 4's end-to-end verification was valuable but discovered issues that should have been caught plan-by-plan.

### Cost Observations
- Sessions: multiple short sessions (plan per session pattern worked well)
- All code phases ran in sonnet; planning in opus
- Plans were small (1–3 files, < 15 min each) — fast iteration, low context accumulation per session

---

## Cross-Milestone Trends

### Process Evolution

| Milestone | Phases | Plans | Key Change |
|-----------|--------|-------|------------|
| v1.1 | 1 | 2 | Established GSD workflow baseline |
| v1.2 | 3 | 11 | Multi-phase milestone; per-plan commit discipline; QA verification plan added as final phase step |

### Top Lessons (Verified Across Milestones)

1. **Small plans (1–3 files) keep sessions focused and reversible** — validated in both v1.1 and v1.2
2. **Build data model before wiring before UI** — the layered phase order eliminated integration surprises
