# State: WindowsHotSpot

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-17)

**Core value:** The mouse hot corner fires Task View reliably every time, on any screen, with zero friction.
**Current focus:** Milestone v1.2 — Per-Corner Actions & Multi-Monitor (Phase 3: Detection Pipeline Multi-Monitor)

## Current Position

Phase: 3 of 4 — Detection Pipeline Multi-Monitor
Plan: 1/3 complete
Status: In Progress
Last activity: 2026-03-18 — Plan 01 complete: MonitorCornerConfig data model + CornerDetector screen-scoped refactor

Progress: [#######---] 62% (Phase 1 complete; Phase 2 complete; Phase 3 Plan 1/3 complete)

## Performance Metrics

**Velocity:**
- Total plans completed: 2 (v1.1)
- Average duration: unknown
- Total execution time: unknown

**By Phase:**

| Phase | Plans | Total | Avg/Plan |
|-------|-------|-------|----------|
| 1. Single-Instance Guard | 2/2 | - | - |

*Updated after each plan completion*

| Phase 02-config-foundation P01 | 3 min | 1 task | 1 file |
| Phase 02-config-foundation P02 | 2 min | 1 task | 1 file |
| Phase 02-config-foundation P03 | 4 min | 2 tasks | 2 files |
| Phase 02-config-foundation P04 | 4 min | 2 tasks | 3 files |
| Phase 03-detection-pipeline-multi-monitor P01 | 8 min | 2 tasks | 2 files |

## Accumulated Context

### Decisions

Decisions are logged in PROJECT.md Key Decisions table.
Recent decisions affecting current work:

- [v1.1]: Use Local\ mutex scope — separate Windows sessions don't block each other
- [v1.1]: Use SendMessage (not PostMessage) for WM_COPYDATA — ensures reliable Settings focus
- [v1.1]: Use NativeWindow + HWND_MESSAGE for IPC target — completely invisible
- [Phase 02-config-foundation]: CornerAction enum and CornerActions dictionary in AppSettings.cs replace single Corner property as v2 data model foundation
- [Phase 02-config-foundation]: LegacyCorner with [JsonPropertyName(Corner)] and WhenWritingNull enables zero-downtime v1 settings.json migration
- [Phase 02-config-foundation]: SaveFile() private helper in ConfigManager writes JSON without firing SettingsChanged — migration safe during startup before listeners attached
- [Phase 02-config-foundation P03]: MakeKeyInput helper intentionally duplicated in ActionDispatcher (two private call sites — not worth shared helper in Phase 2)
- [Phase 02-config-foundation P04]: CornerDetector stores ConfigManager reference — OnDwellComplete reads CornerActions at dispatch time, ensuring live settings without extra UpdateSettings call
- [Phase 03-detection-pipeline-multi-monitor P01]: CornerDetector fields made readonly — immutable at construction enforced by compiler; rebuilt by CornerRouter on settings changes, not mutated in-place
- [Phase 03-detection-pipeline-multi-monitor P01]: MonitorCornerConfig placed in AppSettings.cs (not a new file) — tightly coupled config type kept co-located

### Blockers/Concerns

- [Phase 3]: Screen.DeviceName stability as monitor identity key is MEDIUM confidence — GDI device string can change on driver re-enumeration. Validate early in Phase 3 implementation; fall back to best-match list-of-pairs if needed.
- [Phase 4]: Hotkey recorder edge cases (Win key as modifier, single-key-no-modifier, Ctrl+Alt+Del rejection) need design decisions before Phase 4 coding. Review PowerToys Keyboard Manager for precedent.

## Session Continuity

Last session: 2026-03-18
Stopped at: Completed 03-detection-pipeline-multi-monitor plan 01 (MonitorCornerConfig data model + CornerDetector screen-scoped refactor)
Resume file: None
