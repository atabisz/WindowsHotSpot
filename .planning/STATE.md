# State: WindowsHotSpot

## Project Reference

See: .planning/PROJECT.md (updated 2026-03-17)

**Core value:** The mouse hot corner fires Task View reliably every time, on any screen, with zero friction.
**Current focus:** Milestone v1.2 — Per-Corner Actions & Multi-Monitor (Phase 4: Custom Shortcut Settings UI)

## Current Position

Phase: 4 of 4 — Custom Shortcut Settings UI
Plan: 4/4 complete
Status: Complete
Last activity: 2026-03-18 — Plan 04 complete: end-to-end verification passed; QA-driven fixes added Same-on-all-monitors toggle, Win key recording via WH_KEYBOARD_LL, Win+letter/digit combo support; all Phase 4 requirements confirmed by user

Progress: [##########] 100% (Phase 1 complete; Phase 2 complete; Phase 3 complete; Phase 4 complete 4/4)

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
| Phase 04-custom-shortcut-settings-ui P02 | 1 min | 1 task | 1 file |
| Phase 04-custom-shortcut-settings-ui P03 | 2 min | 2 tasks | 2 files |
| Phase 04-custom-shortcut-settings-ui P04 | 15 min | 2 tasks | 3 files |

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
- [Phase 04-custom-shortcut-settings-ui P02]: PreviewKeyDown Panel subclass chosen over TextBox subclassing — TextBox absorbs Tab/Backspace/Enter as editing actions
- [Phase 04-custom-shortcut-settings-ui P02]: Bare alphanumeric keys without modifiers rejected in-place with advisory text; modifier-only keypresses silently ignored — keeps recorder waiting
- [Phase 04-custom-shortcut-settings-ui P03]: ProcessCmdKey returns false (not true) for Escape-while-recording — false propagates to child KeyRecorderPanel; true would consume it at form level
- [Phase 04-custom-shortcut-settings-ui P03]: SaveGridToConfig called in OnSaveClick (before DialogResult.OK closes form) to flush current monitor's grid state to _pendingMonitorConfigs
- [Phase 04-custom-shortcut-settings-ui P03]: Monitor selector Visible=false (not removed) when single monitor — simpler than conditional control creation
- [Phase 04-custom-shortcut-settings-ui P04]: Win key recording requires WH_KEYBOARD_LL hook — PreviewKeyDown never fires for Win key; OS intercepts before WinForms message queue
- [Phase 04-custom-shortcut-settings-ui P04]: Same-on-all-monitors toggle propagates one MonitorCornerConfig to all screens on save — avoids repeated data entry on symmetric multi-monitor setups

### Blockers/Concerns

- [Phase 3]: Screen.DeviceName stability as monitor identity key is MEDIUM confidence — GDI device string can change on driver re-enumeration. Validate early in Phase 3 implementation; fall back to best-match list-of-pairs if needed.
- [Phase 4]: Hotkey recorder edge cases (Win key as modifier, single-key-no-modifier, Ctrl+Alt+Del rejection) need design decisions before Phase 4 coding. Review PowerToys Keyboard Manager for precedent.

## Session Continuity

Last session: 2026-03-18
Stopped at: Completed 04-custom-shortcut-settings-ui plan 04 (end-to-end verification + QA fixes; Phase 4 and v1.2 milestone complete)
Resume file: None
