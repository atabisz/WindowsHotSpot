---
phase: 04-custom-shortcut-settings-ui
plan: 03
subsystem: ui
tags: [winforms, settings-form, table-layout-panel, key-recorder-panel, multi-monitor, per-corner-actions]

requires:
  - phase: 04-01
    provides: [CustomShortcut-data-model, CornerAction.Custom, MonitorCornerConfig]
  - phase: 04-02
    provides: [KeyRecorderPanel WinForms control, ShortcutRecorded event, RecordingCancelled event]
provides:
  - Redesigned SettingsForm with 2x2 corner grid per monitor
  - Monitor selector ComboBox (hidden when single monitor)
  - Record buttons embedded in each corner cell with KeyRecorderPanel
  - GetMonitorConfigs() API for per-monitor config retrieval
  - HotSpotApplicationContext saves per-monitor MonitorCornerConfig on settings save
affects: []

tech-stack:
  added: []
  patterns:
    - "TableLayoutPanel 2x2 grid for per-corner action UI cells"
    - "ProcessCmdKey override to suppress Escape->CancelButton while KeyRecorderPanel is recording"
    - "_pendingMonitorConfigs dict accumulates cross-monitor edits without mutating caller's settings"
    - "Merge (not replace) pattern in HotSpotApplicationContext preserves disconnected-monitor data (MMON-04)"
    - "Screen.AllScreens captured once in constructor, bound by DeviceName (not index)"

key-files:
  created: []
  modified:
    - WindowsHotSpot/UI/SettingsForm.cs
    - WindowsHotSpot/HotSpotApplicationContext.cs

key-decisions:
  - "Monitor selector hidden (Visible=false) when Screen.AllScreens.Length <= 1, not removed — avoids rebuild on layout"
  - "SaveGridToConfig called in OnSaveClick (before DialogResult.OK closes form) to flush current grid state to pending"
  - "ProcessCmdKey returns false for Escape-while-recording (not true) — false means 'not handled here, let it propagate to child' which is what KeyRecorderPanel needs"
  - "SettingsForm no longer has SelectedCorner property — replaced by GetMonitorConfigs() returning full per-monitor dictionary"

patterns-established:
  - "Full form replacement over incremental patch: confirmed by plan, avoids stale Phase 2 remnants"

requirements-completed:
  - UI-01
  - UI-02
  - CRNA-05

duration: 2min
completed: 2026-03-18
---

# Phase 4 Plan 03: SettingsForm Redesign and HotSpotApplicationContext Wiring Summary

**SettingsForm fully replaced with TableLayoutPanel 2x2 corner grid, per-monitor selector, and embedded KeyRecorderPanel; HotSpotApplicationContext wired to merge per-monitor configs on save.**

## Performance

- **Duration:** ~2 min
- **Started:** 2026-03-18T17:13:32Z
- **Completed:** 2026-03-18T17:15:46Z
- **Tasks:** 2
- **Files modified:** 2

## Accomplishments

- Full SettingsForm replacement: 2x2 TableLayoutPanel grid with GroupBox per corner, action ComboBox, Record button, and embedded KeyRecorderPanel per cell
- ProcessCmdKey override prevents Escape from dismissing the form while any KeyRecorderPanel is recording
- Monitor selector ComboBox (from Screen.AllScreens) shown only when multiple monitors are connected; switches load/save per-monitor data via _pendingMonitorConfigs
- HotSpotApplicationContext.ShowSettingsWindow merges form.GetMonitorConfigs() into Settings.MonitorConfigs (MMON-04 merge, not replace)
- Old SelectedCorner property removed; new public API: GetMonitorConfigs(), SelectedZoneSize, SelectedDwellDelay, SelectedStartWithWindows

## Task Commits

1. **Task 1: Replace SettingsForm with 2x2 corner grid and monitor selector** - `76e94ca` (feat)
2. **Task 2: Update HotSpotApplicationContext to save per-monitor configs** - `86673bd` (feat)

## Files Created/Modified

- `WindowsHotSpot/UI/SettingsForm.cs` — 280-line full replacement; TableLayoutPanel 2x2 grid; per-corner action combo + KeyRecorderPanel + Record button; ProcessCmdKey Escape guard; GetMonitorConfigs() public API
- `WindowsHotSpot/HotSpotApplicationContext.cs` — ShowSettingsWindow updated to call GetMonitorConfigs() and merge into Settings.MonitorConfigs; About dialog copy updated

## Decisions Made

- `ProcessCmdKey` returns `false` (not `true`) when Escape is pressed during recording — `false` means "not handled, propagate to child" which lets KeyRecorderPanel receive it; returning `true` would consume it
- `SaveGridToConfig` is called from an `OnSaveClick` handler (wired to `_saveButton.Click`) before `DialogResult.OK` closes the form, ensuring the current monitor's grid state is persisted to `_pendingMonitorConfigs`
- Monitor selector `Visible = false` when `_screens.Length <= 1` — simpler than conditional control creation, and WinForms handles invisible controls efficiently

## Deviations from Plan

None - plan executed exactly as written.

## Issues Encountered

None.

## Self-Check

- [x] `SettingsForm.cs` replaced: TableLayoutPanel, KeyRecorderPanel, ProcessCmdKey, GetMonitorConfigs(), monitor ComboBox all present
- [x] `HotSpotApplicationContext.cs` updated: GetMonitorConfigs() called, MonitorConfigs merged, no SelectedCorner reference
- [x] `grep -rn "SelectedCorner" WindowsHotSpot/` returns only binary files (no source references)
- [x] Build: 0 errors, 0 warnings
- [x] Commits 76e94ca and 86673bd exist

## Self-Check: PASSED

All expected files modified, all verifications passed, build clean.

## Next Phase Readiness

- All Phase 4 requirements complete: CRNA-05 (custom shortcut dispatch), UI-01 (2x2 corner grid), UI-02 (monitor selector)
- Full end-to-end flow functional: record a shortcut in Settings -> saved to MonitorCornerConfig -> persisted via ConfigManager -> CornerRouter.Rebuild picks it up -> CornerDetector dispatches on dwell

---
*Phase: 04-custom-shortcut-settings-ui*
*Completed: 2026-03-18*
