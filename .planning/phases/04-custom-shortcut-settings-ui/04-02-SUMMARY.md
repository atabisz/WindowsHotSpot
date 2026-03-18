---
phase: 04-custom-shortcut-settings-ui
plan: 02
subsystem: ui
tags: [winforms, keystroke-capture, panel-subclass, preview-key-down, virtual-keys]

requires:
  - phase: 04-01
    provides: [CustomShortcut-data-model, CornerAction.Custom]

provides:
  - KeyRecorderPanel WinForms control
  - ShortcutRecorded event with ushort[] VK sequence and string display text
  - RecordingCancelled event on bare Escape

affects:
  - 04-03-SettingsForm (embeds KeyRecorderPanel to record shortcuts)

tech-stack:
  added: []
  patterns:
    - "Panel subclass with SetStyle(ControlStyles.Selectable) for keyboard focus"
    - "PreviewKeyDown.IsInputKey=true intercepts Tab/Escape/arrow/Enter before WinForms dialog routing"
    - "Left-side VK constants (0xA0/0xA2/0xA4) for modifier keys in SendInput arrays"
    - "KeysConverter.ConvertToString for human-readable shortcut labels without hand-rolled lookup"

key-files:
  created:
    - WindowsHotSpot/UI/KeyRecorderPanel.cs
  modified: []

key-decisions:
  - "PreviewKeyDown approach chosen over subclassing TextBox — TextBox intercepts Tab/Backspace/Enter as editing actions; Panel has no such built-in behaviour"
  - "Bare alphanumeric keys (A-Z, 0-9) without modifiers are rejected with inline advisory text — they would fire on every dwell and interfere with normal typing"
  - "Modifier-only keypresses (Shift, Ctrl, Alt alone) are ignored without cancelling — recorder keeps waiting for main key, matching standard shortcut recorder UX"
  - "Win key limitation not surfaced in this control — Win+key absorbed by Windows shell before WM_KEYDOWN; SettingsForm (04-03) will show form-level advisory"
  - "DisplayText built at record time via KeysConverter, not regenerated from VKs — consistent with CustomShortcut design decision from 04-01"

patterns-established:
  - "Panel subclass for WinForms keystroke capture: SetStyle Selectable, PreviewKeyDown IsInputKey, OnKeyDown with SuppressKeyPress"

requirements-completed:
  - CRNA-05

duration: 1min
completed: 2026-03-18
---

# Phase 4 Plan 02: KeyRecorderPanel Summary

**Focusable Panel subclass that captures arbitrary key combinations via PreviewKeyDown interception and fires (ushort[], string) events ready for CustomShortcut construction.**

## Performance

- **Duration:** ~1 min
- **Started:** 2026-03-18T17:10:28Z
- **Completed:** 2026-03-18T17:11:23Z
- **Tasks:** 1
- **Files modified:** 1

## Accomplishments

- `KeyRecorderPanel` — sealed Panel subclass with focusable recording mode
- All key classes handled: navigation keys (Tab/arrows/Enter), Escape (cancel), modifier-only (keep waiting), bare alphanumeric (reject with advisory), everything else (commit)
- Left-side VK codes (0xA2/0xA0/0xA4) consistent with existing ActionDispatcher pattern
- `BuildDisplayText` uses `KeysConverter.ConvertToString` — no hand-rolled name table
- Zero build errors, zero warnings

## Task Commits

1. **Task 1: Create KeyRecorderPanel with full keystroke capture logic** - `1ed833e` (feat)

## Files Created/Modified

- `WindowsHotSpot/UI/KeyRecorderPanel.cs` — 162-line Panel subclass; ShortcutRecorded and RecordingCancelled events; StartRecording/CancelRecording methods; inline validation messaging

## Decisions Made

- PreviewKeyDown approach used over TextBox subclassing — TextBox absorbs Tab/Backspace/Enter as editing keys; Panel has no built-in key routing that would interfere
- Bare alphanumeric keys without modifiers rejected in-place with advisory text rather than cancelling — user can add a modifier and try again without restarting recording
- Win key limitation silently absent from this control; SettingsForm (04-03) responsible for advisory text

## Deviations from Plan

None — plan executed exactly as written.

## Issues Encountered

None.

## Next Phase Readiness

- `KeyRecorderPanel` is ready to embed in `SettingsForm` (04-03)
- Fires `ShortcutRecorded(ushort[] vks, string displayText)` — SettingsForm constructs `new CustomShortcut(vks, displayText)` from event args
- `IdleText` property allows SettingsForm to display current shortcut label when not recording

---
*Phase: 04-custom-shortcut-settings-ui*
*Completed: 2026-03-18*
