---
phase: 12-wiring-settings
plan: "01"
subsystem: wiring
tags: [transparency, settings, wiring, disposal]
dependency_graph:
  requires: [11-01]
  provides: [WindowTransparencyHandler wired at runtime, TransparencyStep configurable in Settings]
  affects: [HotSpotApplicationContext, SettingsForm]
tech_stack:
  added: []
  patterns: [OR-lambda WheelSuppressionPredicate combination, MouseWheeled dual-subscribe, NumericUpDown row pattern]
key_files:
  created: []
  modified:
    - WindowsHotSpot/HotSpotApplicationContext.cs
    - WindowsHotSpot/UI/SettingsForm.cs
decisions:
  - "WheelSuppressionPredicate combined via OR lambda (not +=) — single-slot Func<int,bool>? field; both predicates evaluated per wheel event"
  - "Both MouseWheeled handlers unsubscribed before WheelSuppressionPredicate = null in DisposeComponents — T-12-03 mitigation (stale lambda)"
  - "Window Interactions group height 48→78px; buttonPanelTop offset 56→86 (30px cascade); ClientSize formula unchanged"
  - "TransparencyStep NumericUpDown range 1-50, increment 1, default clamped 1-50 per TRNSP-04"
metrics:
  duration: "< 5 minutes"
  completed: "2026-05-06"
  tasks_completed: 3
  tasks_total: 3
  files_modified: 2
---

# Phase 12 Plan 01: Wiring + Settings Summary

**One-liner:** WindowTransparencyHandler wired into HotSpotApplicationContext with OR-lambda WheelSuppressionPredicate and TransparencyStep NumericUpDown (1-50) added to SettingsForm Window Interactions group.

## Tasks Completed

| Task | Name | Commit | Files |
|------|------|--------|-------|
| 1 | Wire WindowTransparencyHandler into HotSpotApplicationContext | 91b5adc | HotSpotApplicationContext.cs |
| 2 | Add Transparency Step row to SettingsForm Window Interactions group | 00c19c1 | SettingsForm.cs |
| 3 | Build verification | (no code changes) | — |

## What Was Built

### HotSpotApplicationContext.cs

Four targeted edits:

1. **Field declaration:** `private readonly WindowTransparencyHandler _windowTransparencyHandler;` added after `_scrollResizeHandler`.

2. **Constructor wiring:** After `_scrollResizeHandler` wiring block, added:
   - `_windowTransparencyHandler = new WindowTransparencyHandler(_configManager.Settings)`
   - `_hookManager.MouseWheeled += _windowTransparencyHandler.OnMouseWheeled`
   - OR-lambda replacing the old single-predicate `WheelSuppressionPredicate` assignment
   - `_windowTransparencyHandler.Install()`

3. **Settings save (ShowSettingsWindow OK branch):** `_configManager.Settings.TransparencyStep = form.SelectedTransparencyStep` inserted after `ScrollResizeStep` assignment.

4. **DisposeComponents:** Both MouseWheeled handlers unsubscribed before `WheelSuppressionPredicate = null`, then `_windowTransparencyHandler.Dispose()` called after `_scrollResizeHandler.Dispose()`.

### SettingsForm.cs

Four targeted edits:

1. **Public property:** `public int SelectedTransparencyStep => (int)_transparencyStepInput.Value;` added after `SelectedScrollResizeStep`.

2. **Field declaration:** `private readonly NumericUpDown _transparencyStepInput;` added after `_scrollResizeStepInput`.

3. **Window Interactions group expanded:** Height 48→78px; second row added — label "Transparency step:" (x=12, y=54), NumericUpDown (x=136, y=51, width=65, range 1-50, increment 1, default clamped), unit label "α / notch" (x=209, y=54).

4. **buttonPanelTop offset updated:** `windowInteractionsGroupTop + 56` → `windowInteractionsGroupTop + 86` (+30px matching group height growth).

## Deviations from Plan

None — plan executed exactly as written.

## Build Result

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## Threat Surface Scan

No new network endpoints, auth paths, file access patterns, or schema changes at trust boundaries introduced. T-12-03 mitigation (unsubscribe ordering before predicate null-out) confirmed present in DisposeComponents.

## Known Stubs

None — `_transparencyStepInput` is wired to `settings.TransparencyStep` for both read (initial value clamped) and write (saved on OK).

## Self-Check: PASSED

- `WindowsHotSpot/HotSpotApplicationContext.cs` exists and contains `_windowTransparencyHandler` (7 occurrences)
- `WindowsHotSpot/UI/SettingsForm.cs` exists and contains `_transparencyStepInput` (4 occurrences) and `SelectedTransparencyStep` (1 occurrence)
- Commits 91b5adc and 00c19c1 exist
- Build succeeded with 0 errors
