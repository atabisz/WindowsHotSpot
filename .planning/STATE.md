# State: WindowsHotSpot

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-17)

**Core value:** The mouse hot corner fires Task View reliably every time, on any screen, with zero friction.
**Current focus:** Milestone v1.2 — Per-Corner Actions & Multi-Monitor (Phase 4: Custom Shortcut Settings UI)

## Current Position

Phase: 4 of 4 — Custom Shortcut Settings UI
Plan: 1/4 complete
Status: In Progress
Last activity: 2026-03-18 — Plan 01 complete: CustomShortcut data model and dispatch pipeline wired; CornerAction.Custom serialises and fires arbitrary key sequences

Progress: [##########] 100% (Phase 1 complete; Phase 2 complete; Phase 3 complete; Phase 4 in progress 1/4)

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
| Phase 03-detection-pipeline-multi-monitor P02 | 2 | 1 tasks | 1 files |
| Phase 03-detection-pipeline-multi-monitor P03 | 10 min | 2 tasks | 1 files |
| Phase 04-custom-shortcut-settings-ui P01 | 5 min | 2 tasks | 4 files |

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
- [Phase 03-detection-pipeline-multi-monitor]: CornerRouter uses record struct ScreenDetectors for named screen+detector-list pairing; pre-caches Screen.AllScreens in Rebuild() to keep OnMouseMoved hot-path P/Invoke-free
- [Phase 03-detection-pipeline-multi-monitor P03]: SystemEvents.DisplaySettingsChanged unsubscribed first in DisposeComponents() before component disposal — static events hold strong references and must be released to prevent memory leak
- [Phase 04-custom-shortcut-settings-ui]: CustomShortcut uses ushort[] VirtualKeys (not Keys[]) and stores DisplayText to avoid VK-to-Keys remapping on deserialise
- [Phase 04-custom-shortcut-settings-ui]: CustomShortcuts lives in MonitorCornerConfig only (not global AppSettings) per research architecture

### Blockers/Concerns

- [Phase 3]: Screen.DeviceName stability as monitor identity key is MEDIUM confidence — GDI device string can change on driver re-enumeration. Validate early in Phase 3 implementation; fall back to best-match list-of-pairs if needed.
- [Phase 4]: Hotkey recorder edge cases (Win key as modifier, single-key-no-modifier, Ctrl+Alt+Del rejection) need design decisions before Phase 4 coding. Review PowerToys Keyboard Manager for precedent.

## Session Continuity

Last session: 2026-03-18
Stopped at: Completed 04-custom-shortcut-settings-ui plan 01 (CustomShortcut data model and dispatch pipeline)
Resume file: None
