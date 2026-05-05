---
phase: 06-window-drag-handler
plan: 02
subsystem: config-and-settings-ui
tags: [appsettings, settingsform, winforms, drag-passthrough]
dependency_graph:
  requires:
    - 06-01-PLAN.md  # NativeMethods P/Invoke additions (parallel wave)
  provides:
    - AppSettings.WindowDragPassThrough bool field for JSON serialisation
    - SettingsForm "Window Dragging" GroupBox with checkbox
    - SettingsForm.SelectedWindowDragPassThrough public property
  affects:
    - WindowsHotSpot/Config/AppSettings.cs
    - WindowsHotSpot/UI/SettingsForm.cs
tech_stack:
  added: []
  patterns:
    - Auto-property bool field added to AppSettings POCO (same style as SameOnAllMonitors)
    - GroupBox + CheckBox added to SettingsForm manual WinForms layout
    - buttonPanelTop arithmetic chain extended by one group (windowDragGroupTop = systemGroupTop + 56)
key_files:
  created: []
  modified:
    - WindowsHotSpot/Config/AppSettings.cs
    - WindowsHotSpot/UI/SettingsForm.cs
decisions:
  - WindowDragPassThrough placed after SameOnAllMonitors and before MonitorConfigs in AppSettings — consistent alphabetical-ish group ordering of bool flags
  - No [JsonPropertyName] attribute — property name is unambiguous for JSON round-trip
  - windowDragGroup declared as local variable (not private field) — only referenced during construction, no post-construction access needed
  - buttonPanelTop formula changed from systemGroupTop+56 to windowDragGroupTop+56 — net shift of +56px on ClientSize.Height
metrics:
  duration: "~5 min"
  completed: "2026-05-05"
  tasks_completed: 2
  tasks_total: 2
  files_modified: 2
---

# Phase 6 Plan 02: AppSettings WindowDragPassThrough and SettingsForm Window Dragging Section Summary

**One-liner:** AppSettings gains `WindowDragPassThrough` bool (default false) and SettingsForm gains a 396x48 "Window Dragging" GroupBox with passthrough checkbox, shifting the button panel down 56px.

---

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Add WindowDragPassThrough property to AppSettings | 800ec7a | WindowsHotSpot/Config/AppSettings.cs |
| 2 | Add "Window Dragging" section to SettingsForm | 02426e3 | WindowsHotSpot/UI/SettingsForm.cs |

---

## What Was Built

### Task 1: AppSettings.WindowDragPassThrough

Added `public bool WindowDragPassThrough { get; set; } = false;` to `AppSettings` after `SameOnAllMonitors` and before `MonitorConfigs`. The property follows the same auto-property pattern as peer bool fields, has no `[JsonPropertyName]` attribute (name is unambiguous), and round-trips to `settings.json`. This is the field `WindowDragHandler` will read at drag-decision time to determine whether to swallow or pass through clicks on non-draggable surfaces (D-04).

### Task 2: SettingsForm "Window Dragging" Section

Four changes to `SettingsForm.cs`:

1. **Private field** `_windowDragPassThroughCheckBox` added after `_startupCheckBox`
2. **Public property** `SelectedWindowDragPassThrough => _windowDragPassThroughCheckBox.Checked` added after `SelectedSameOnAllMonitors`
3. **GroupBox construction** inserted after `systemGroup.Controls.Add(_startupCheckBox)`:
   - `windowDragGroupTop = systemGroupTop + 56` (8px gap below System group, matching existing group-to-group spacing)
   - GroupBox: `Text = "Window Dragging"`, `Size = new Size(396, 48)` — matches `systemGroup` dimensions exactly
   - CheckBox: `Text = "Pass through clicks when no window is draggable"`, `AutoSize = true`, `Checked = settings.WindowDragPassThrough`
4. **buttonPanelTop** changed from `systemGroupTop + 56` to `windowDragGroupTop + 56` — form ClientSize.Height increases by 56px automatically; `Controls.AddRange` updated to include `windowDragGroup` between `systemGroup` and `buttonPanel`

---

## Deviations from Plan

None — plan executed exactly as written.

---

## Stub Tracking

No stubs. The `Checked = settings.WindowDragPassThrough` initializer reads the actual settings value. The `SelectedWindowDragPassThrough` property returns the live checkbox value. No placeholder text or hardcoded empty data.

Phase 7 (wiring) will connect `SelectedWindowDragPassThrough` to `ConfigManager` on Save — that connection is explicitly out of scope for this plan, but the property is ready to be consumed.

---

## Threat Surface Scan

No new network endpoints, auth paths, file access patterns, or schema changes at trust boundaries were introduced beyond what the plan's threat model covers (T-06-03: JSON bool round-trip, T-06-04: in-process settings read).

---

## Self-Check

- AppSettings.cs: FOUND
- SettingsForm.cs: FOUND
- 06-02-SUMMARY.md: FOUND
- Commit 800ec7a: FOUND
- Commit 02426e3: FOUND

## Self-Check: PASSED
