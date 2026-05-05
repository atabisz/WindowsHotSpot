---
phase: 06-window-drag-handler
reviewed: 2026-05-05T00:00:00Z
depth: standard
files_reviewed: 5
files_reviewed_list:
  - WindowsHotSpot/Native/NativeMethods.cs
  - WindowsHotSpot/Config/AppSettings.cs
  - WindowsHotSpot/UI/SettingsForm.cs
  - WindowsHotSpot/Core/WindowDragHandler.cs
  - WindowsHotSpot/HotSpotApplicationContext.cs
findings:
  critical: 2
  warning: 5
  info: 0
  total: 7
status: issues_found
---

# Phase 06: Code Review Report

**Reviewed:** 2026-05-05
**Depth:** standard
**Files Reviewed:** 5
**Status:** issues_found

## Summary

Reviewed the five files comprising the Phase 06 window-drag-handler implementation: `NativeMethods.cs`, `AppSettings.cs`, `SettingsForm.cs`, `WindowDragHandler.cs`, and `HotSpotApplicationContext.cs`. Cross-referenced `HookManager.cs` to verify the suppression predicate contract.

Two blockers were found. The more impactful is that `WindowDragPassThrough` is never written back from the settings form to `AppSettings`, so the user's choice is silently discarded and the feature always behaves as if the checkbox is unchecked. The second blocker is that only LBUTTONDOWN is suppressed on a modifier+click on a non-draggable surface — LBUTTONUP is always passed through, delivering an unmatched mouse-up to target windows.

Five warnings cover: a `NumericUpDown` value range crash, an inconsistent click-suppression gap when `GetWindowRect` fails, a missing double-dispose guard on `DisposeComponents`, an unsubscribed `SettingsChanged` event, and dead code duplication between `EndDrag`/`CancelDragIfActive`.

---

## Critical Issues

### CR-01: `WindowDragPassThrough` setting never saved back to `AppSettings`

**File:** `WindowsHotSpot/HotSpotApplicationContext.cs:126-139`

**Issue:** `SettingsForm` exposes `SelectedWindowDragPassThrough` (line 46 of `SettingsForm.cs`) and `AppSettings` has the `WindowDragPassThrough` property, but `ShowSettingsWindow` never copies the form value back to `_configManager.Settings` before calling `_configManager.Save()`. The property is unconditionally read by `WindowDragHandler.BeginDragAttempt` on every modifier+click, so the user's checkbox selection has zero effect — the handler always uses the initial (file-loaded) value from startup. Because `_configManager.Save()` serializes the unchanged value, the setting is also never persisted.

**Fix:**
```csharp
// In ShowSettingsWindow(), inside the if (form.ShowDialog() == DialogResult.OK) block,
// add this line alongside the other settings assignments:
_configManager.Settings.WindowDragPassThrough = form.SelectedWindowDragPassThrough;
```

---

### CR-02: LBUTTONUP is not suppressed when LBUTTONDOWN was suppressed (orphaned mouse-up)

**File:** `WindowsHotSpot/Core/WindowDragHandler.cs:77-83`

**Issue:** `_suppressNextClick` is a one-shot flag designed to suppress "the initiating click pair" (comment at line 25) when a modifier+click lands on a non-draggable surface with `WindowDragPassThrough=false`. In practice it only suppresses LBUTTONDOWN.

Trace through `HookManager.HookCallback`:

1. **LBUTTONDOWN fires** — `MouseButtonChanged.Invoke(true)` calls `BeginDragAttempt`, which sets `_suppressNextClick = true`. Then `SuppressionPredicate.Invoke(WM_LBUTTONDOWN)` calls `ShouldSuppress`: `_isDragging` is false, `_suppressNextClick` is true → flag is cleared to `false`, returns `true`. LBUTTONDOWN is suppressed.
2. **LBUTTONUP fires** — `MouseButtonChanged.Invoke(false)` calls `EndDrag`, a no-op since `_isDragging` is false. Then `SuppressionPredicate.Invoke(WM_LBUTTONUP)` calls `ShouldSuppress`: `_isDragging` is false, `_suppressNextClick` is false (was cleared in step 1) → returns `false`. LBUTTONUP is **not** suppressed.

The target window therefore receives an LBUTTONUP with no preceding LBUTTONDOWN. Many controls (drag-and-drop handlers, splitters, custom renderers) misbehave or enter broken state when they see an unmatched button-up.

**Fix:** Keep the flag alive through LBUTTONUP by introducing a second flag, or by making `ShouldSuppress` check the message type before clearing:

```csharp
// In ShouldSuppress — clear only on WM_LBUTTONUP so both messages are suppressed:
if (_suppressNextClick)
{
    if (msg == NativeMethods.WM_LBUTTONUP)
        _suppressNextClick = false;  // clear after the full pair is suppressed
    return true;
}
```

---

## Warnings

### WR-01: `NumericUpDown.Value` throws if loaded settings are out of range

**File:** `WindowsHotSpot/UI/SettingsForm.cs:245-260`

**Issue:** `NumericUpDown.Value` throws `ArgumentOutOfRangeException` if the assigned value is outside `[Minimum, Maximum]`. `_dwellDelayInput` has `Minimum = 50`; `_zoneSizeInput` has `Minimum = 1`. A `settings.json` containing a valid but out-of-range value (e.g. `"DwellDelayMs": 10`, `"ZoneSize": 0`) passes `JsonSerializer.Deserialize` successfully — `ConfigManager.Load`'s `catch` block does not trigger — and the constructor of `SettingsForm` crashes with an unhandled exception.

**Fix:** Clamp values before assigning to avoid the throw:

```csharp
Value = Math.Clamp(settings.ZoneSize, 1, 50),      // for _zoneSizeInput
Value = Math.Clamp(settings.DwellDelayMs, 50, 2000), // for _dwellDelayInput
```

---

### WR-02: `GetWindowRect` failure in `BeginDragAttempt` does not set `_suppressNextClick`

**File:** `WindowsHotSpot/Core/WindowDragHandler.cs:143-145`

**Issue:** Steps 2 and 3 of `BeginDragAttempt` (failed `GetAncestor` / failed `GetWindowPlacement`) both apply `_suppressNextClick = true` when `WindowDragPassThrough=false`. Step 4 (`GetWindowRect` failure) silently `return`s without doing the same. If `GetWindowRect` fails, the code abandons the drag attempt but the modifier+click is passed through to the window underneath — inconsistent with the suppression contract established at the earlier steps.

**Fix:**
```csharp
// Replace the bare return at line 144-145 with:
if (!NativeMethods.GetWindowRect(rootHwnd, out var rect))
{
    if (!_settings.WindowDragPassThrough) _suppressNextClick = true;
    return;
}
```

---

### WR-03: `DisposeComponents` has no double-dispose guard

**File:** `WindowsHotSpot/HotSpotApplicationContext.cs:175-201`

**Issue:** `DisposeComponents` is called from two code paths: `Dispose(bool disposing)` and `OnApplicationExit` (subscribed to `Application.ApplicationExit`). There is no `_disposed` flag to make the method idempotent. If both fire (e.g., a caller disposes the context explicitly while `Application.ApplicationExit` also fires), `_ipcWindow.Dispose()` calls `DestroyHandle()` twice; `_trayIcon.Dispose()` and `_contextMenu.Dispose()` are called twice; and event unsubscriptions are attempted on already-unsubscribed delegates. `NativeWindow.DestroyHandle()` on an already-destroyed handle is documented as a no-op, but `NotifyIcon.Dispose` and `ContextMenuStrip.Dispose` are not guaranteed to be idempotent.

**Fix:**
```csharp
private bool _disposed;

private void DisposeComponents()
{
    if (_disposed) return;
    _disposed = true;
    // ... existing body ...
}
```

---

### WR-04: `SettingsChanged` lambda is never unsubscribed

**File:** `WindowsHotSpot/HotSpotApplicationContext.cs:86`

**Issue:** The subscription `_configManager.SettingsChanged += () => _cornerRouter.Rebuild(_configManager.Settings)` captures both `_cornerRouter` and `_configManager` in a closure. It is never removed in `DisposeComponents`. This is not a GC leak (both objects are owned by the same class and collected together), but it means that if `_configManager.Save()` is called after `_cornerRouter` has been disposed, `CornerRouter.Rebuild` will be invoked on a disposed object, potentially causing undefined behaviour.

**Fix:** Store the lambda in a field and unsubscribe it in `DisposeComponents`:

```csharp
// In the constructor:
_onSettingsChanged = () => _cornerRouter.Rebuild(_configManager.Settings);
_configManager.SettingsChanged += _onSettingsChanged;

// Field declaration:
private readonly Action _onSettingsChanged;

// In DisposeComponents(), before _cornerRouter.Dispose():
_configManager.SettingsChanged -= _onSettingsChanged;
```

---

### WR-05: `EndDrag` and `CancelDragIfActive` duplicate identical cleanup logic

**File:** `WindowsHotSpot/Core/WindowDragHandler.cs:157-173`

**Issue:** `EndDrag` and `CancelDragIfActive` perform identical work — set `_isDragging = false`, set `_dragTarget = IntPtr.Zero`, call `SetCursor(_arrowCursor)` — with only a trivial structural difference (early return vs. `if` guard). Both are private. `CancelDragIfActive` is called only from `KeyboardCallback` on modifier key-up. If cursor-restore logic or additional cleanup (e.g., posting a synthetic mouse-up) is added to one method in the future, the other will diverge silently.

**Fix:** Eliminate the duplication by having `CancelDragIfActive` delegate to `EndDrag` (or vice versa), since the behavioural intent is the same:

```csharp
private void CancelDragIfActive()
{
    // Window stays at current (mid-drag) position — no position reset
    EndDrag();
}
```

---

_Reviewed: 2026-05-05_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_
