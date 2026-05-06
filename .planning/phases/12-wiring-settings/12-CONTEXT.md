# Phase 12: Wiring + Settings - Context

**Gathered:** 2026-05-06
**Status:** Ready for planning

<domain>
## Phase Boundary

Wire `WindowTransparencyHandler` into `HotSpotApplicationContext` so it is active at runtime, add the `TransparencyStep` control to the Settings form "Window Interactions" group, and handle clean disposal on exit.

</domain>

<decisions>
## Implementation Decisions

### HotSpotApplicationContext Wiring
- **D-01:** Add `_windowTransparencyHandler` as a class-level `readonly` field alongside `_scrollResizeHandler` and `_alwaysOnTopHandler`.
- **D-02:** Wire `_hookManager.MouseWheeled += _windowTransparencyHandler.OnMouseWheeled` (same pattern as ScrollResizeHandler line 89).
- **D-03:** `WheelSuppressionPredicate` is a single-slot field — combine both predicates via OR lambda:
  `_hookManager.WheelSuppressionPredicate = msg => _scrollResizeHandler.ShouldSuppressWheel(msg) || _windowTransparencyHandler.ShouldSuppressWheel(msg);`
  This replaces the existing single-predicate assignment (line 90).
- **D-04:** Call `_windowTransparencyHandler.Install()` after wiring (mirrors ScrollResizeHandler line 91).
- **D-05:** In `DisposeComponents()`: unsubscribe `MouseWheeled`, set `WheelSuppressionPredicate = null` (clearing both — single field), call `_windowTransparencyHandler.Dispose()`. The WheelSuppressionPredicate null-out already happens on ScrollResizeHandler dispose path; Phase 12 just ensures it stays null after both are unsubscribed.
- **D-06:** In `ShowSettingsWindow()` OK branch: read `form.SelectedTransparencyStep` and assign to `_configManager.Settings.TransparencyStep` (same pattern as `ScrollResizeStep` on line 144).

### Settings Form
- **D-07:** Expand the existing "Window Interactions" group — increase its height from 48px to 78px to accommodate a second row.
- **D-08:** Add a second row to the "Window Interactions" group: label "Transparency step:", `NumericUpDown` (Minimum=1, Maximum=50, Increment=1, default=settings.TransparencyStep), unit label "α / notch".
- **D-09:** Label x-position: align with existing "Scroll resize step:" label (x=12). NumericUpDown x=136, width=65 (same as `_scrollResizeStepInput`). Unit label x=209.
- **D-10:** NumericUpDown y=51 (first row y=21, row spacing=30). Unit label y=54.
- **D-11:** Public property: `public int SelectedTransparencyStep => (int)_transparencyStepInput.Value;`
- **D-12:** Range 1–50, increment 1, default `settings.TransparencyStep` (clamped 1–50) — matches TRNSP-04.

### Claude's Discretion
- `buttonPanelTop` and `ClientSize` height cascade from `windowInteractionsGroupTop + 56` today; Phase 12 must update to `windowInteractionsGroupTop + 86` (30px taller group) to keep layout consistent.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Primary analogs (read these in full)
- `WindowsHotSpot/HotSpotApplicationContext.cs` — wiring pattern: lines 88-91 (ScrollResizeHandler) and 209-211 (dispose). Phase 12 mirrors these exactly.
- `WindowsHotSpot/UI/SettingsForm.cs` — "Window Interactions" group (lines 313-337), `SelectedScrollResizeStep` public property (line 47), `buttonPanelTop` calculation (line 340), `ClientSize` assignment (line 374).
- `WindowsHotSpot/Core/ScrollResizeHandler.cs` — `ShouldSuppressWheel` signature (Phase 12 calls both).
- `WindowsHotSpot/Core/WindowTransparencyHandler.cs` — `ShouldSuppressWheel`, `OnMouseWheeled`, `Install`, `Dispose` — the handler being wired.

### Requirements
- `.planning/REQUIREMENTS.md` — TRNSP-04 (step range 1–50), WIRE-01 (wiring), WIRE-02 (disposal)

### No external specs — decisions fully captured above.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `ScrollResizeHandler` wiring block (HotSpotApplicationContext lines 88-91): exact template for WindowTransparencyHandler wiring
- `SelectedScrollResizeStep` public property pattern (SettingsForm line 47): template for `SelectedTransparencyStep`
- `_scrollResizeStepInput` NumericUpDown (SettingsForm lines 326-332): template for transparency step input — same location, width, y-offset

### Established Patterns
- WheelSuppressionPredicate is a `Func<int, bool>?` single-slot field — NOT an event. Combining requires explicit lambda OR, not `+=`.
- Settings apply on Save only (ShowDialog → OK branch → assign fields → _configManager.Save()). No live update.
- `MakeLabel(text, x, y)` helper (SettingsForm line 572) used for all group labels — use it for transparency row labels too.
- `Math.Clamp(settings.X, min, max)` pattern for NumericUpDown initial value.

### Integration Points
- `HotSpotApplicationContext` constructor: after `_scrollResizeHandler.Install()` line (line 91) — add transparency handler wiring.
- `HotSpotApplicationContext.DisposeComponents()`: after `_scrollResizeHandler.Dispose()` line (line 211) — unsubscribe and dispose transparency handler.
- `HotSpotApplicationContext.ShowSettingsWindow()`: after `ScrollResizeStep` assignment (line 144) — add `TransparencyStep` assignment.
- `SettingsForm` "Window Interactions" group: expand height, add second row below existing scroll resize row.

</code_context>

<specifics>
## Specific Ideas

- The "Window Interactions" group must grow from 48px to 78px height. The `buttonPanelTop` variable that follows must update from `windowInteractionsGroupTop + 56` to `windowInteractionsGroupTop + 86` to preserve spacing.
- `ClientSize` height is computed from `buttonPanelTop + 44` — no change to that formula, just `buttonPanelTop` is larger.

</specifics>

<deferred>
## Deferred Ideas

None — discussion stayed within phase scope.

</deferred>

---

*Phase: 12-Wiring + Settings*
*Context gathered: 2026-05-06*
