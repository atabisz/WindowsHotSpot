# Phase 12 Code Review

**Files reviewed:** WindowsHotSpot/HotSpotApplicationContext.cs, WindowsHotSpot/UI/SettingsForm.cs
**Depth:** standard
**Date:** 2026-05-06

## Summary

Phase 12 correctly wires `WindowTransparencyHandler` into the application context, correctly chains the `WheelSuppressionPredicate` via an OR-lambda, persists `TransparencyStep` through the settings save path, and expands the `windowInteractionsGroup` layout with arithmetically consistent spacing. One behavioral defect is present: the `ScrollResizeHandler` modifier gate does not exclude the Shift key, so `Ctrl+Alt+Shift+Scroll` triggers both the resize and the transparency operations simultaneously. Phase 12 wiring is what makes this conflict live; prior to this phase the transparency handler was not connected.

## Findings

### Critical

None

---

### Warning

**WR-01: `Ctrl+Alt+Shift+Scroll` fires both `ScrollResizeHandler` and `WindowTransparencyHandler` concurrently**

**File:** `WindowsHotSpot/HotSpotApplicationContext.cs:90-96` (wiring site); root cause at `WindowsHotSpot/Core/ScrollResizeHandler.cs:59`

**Issue:** `HookManager.MouseWheeled` fires all subscribers before `WheelSuppressionPredicate` is consulted. Both handlers are subscribed unconditionally:

```csharp
// HotSpotApplicationContext.cs lines 90-93
_hookManager.MouseWheeled += _scrollResizeHandler.OnMouseWheeled;
_hookManager.MouseWheeled += _windowTransparencyHandler.OnMouseWheeled;
```

`ScrollResizeHandler.OnMouseWheeled` gates only on `_lCtrlDown && _lAltDown`; it has no `!_lShiftDown` guard. `WindowTransparencyHandler.OnMouseWheeled` gates on `_lCtrlDown && _lAltDown && _lShiftDown`. When the user holds `Ctrl+Alt+Shift` and scrolls, both gates pass and both handlers execute — the window is simultaneously resized and made more/less transparent. The `WheelSuppressionPredicate` OR-lambda correctly suppresses the underlying wheel event from reaching the target window, but that does not prevent the double side-effect: both `OnMouseWheeled` handlers ran before the predicate was even consulted.

**Fix:** Add a `!_lShiftDown` exclusion to `ScrollResizeHandler.OnMouseWheeled` and `ShouldSuppressWheel` so the resize handler yields to the transparency handler when all three modifiers are held:

```csharp
// ScrollResizeHandler.cs — OnMouseWheeled (line 59)
if (!_lCtrlDown || !_lAltDown || _lShiftDown) return;  // yield to transparency handler when Shift held

// ScrollResizeHandler.cs — ShouldSuppressWheel (line 50)
public bool ShouldSuppressWheel(int msg) => _lCtrlDown && _lAltDown && !_lShiftDown;
```

With this change the OR-lambda in `HotSpotApplicationContext.cs` remains correct and the two gestures become non-overlapping: `Ctrl+Alt+Scroll` → resize only; `Ctrl+Alt+Shift+Scroll` → transparency only.

---

### Info

**IN-01: `SettingsForm` constructor sets `ClientSize` twice**

**File:** `WindowsHotSpot/UI/SettingsForm.cs:98` and `SettingsForm.cs:404`

**Issue:** `ClientSize` is assigned an arbitrary placeholder value (`new Size(420, 500)`) at line 98, then overwritten with the correctly computed value at line 404. The first assignment is dead code.

**Fix:** Remove the placeholder assignment at line 98. The form's `ClientSize` is fully determined by `buttonPanelTop + 44` at line 404; no interim value is needed.

**IN-02: `_configManager` never unsubscribed from `Application.ApplicationExit`**

**File:** `WindowsHotSpot/HotSpotApplicationContext.cs:113` and `DisposeComponents` (lines 199-234)

**Issue:** `Application.ApplicationExit += OnApplicationExit` is never removed in `DisposeComponents`. Because `Application.ApplicationExit` is a static event, it holds a reference to the `HotSpotApplicationContext` instance after disposal. In practice this is benign since the process exits immediately after, but it is inconsistent with the careful `SystemEvents.DisplaySettingsChanged` unsubscription that is explicitly called out in a comment on line 207-208.

**Fix:** Add `Application.ApplicationExit -= OnApplicationExit;` near the top of `DisposeComponents`, alongside the `SystemEvents.DisplaySettingsChanged` unsubscription.

## Verdict

PASS_WITH_NOTES
