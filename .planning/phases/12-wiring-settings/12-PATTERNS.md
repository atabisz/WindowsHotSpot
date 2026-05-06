# Phase 12: Wiring + Settings - Pattern Map

**Mapped:** 2026-05-06
**Files analyzed:** 2
**Analogs found:** 2 / 2

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match Quality |
|---|---|---|---|---|
| `WindowsHotSpot/HotSpotApplicationContext.cs` | wiring/context | event-driven | Same file — ScrollResizeHandler block (lines 88-91, 209-212) | exact |
| `WindowsHotSpot/UI/SettingsForm.cs` | component/form | request-response | Same file — Window Interactions group (lines 323-348), public property (line 48) | exact |

---

## Pattern Assignments

### `WindowsHotSpot/HotSpotApplicationContext.cs` (wiring, event-driven)

**Analog:** Same file — `_scrollResizeHandler` block

---

#### Field declaration pattern (lines 24-25)

```csharp
private readonly ScrollResizeHandler _scrollResizeHandler;
private readonly AlwaysOnTopHandler _alwaysOnTopHandler;
```

Add `_windowTransparencyHandler` in the same field list, immediately after `_scrollResizeHandler`:

```csharp
private readonly ScrollResizeHandler _scrollResizeHandler;
private readonly WindowTransparencyHandler _windowTransparencyHandler;
private readonly AlwaysOnTopHandler _alwaysOnTopHandler;
```

---

#### Constructor wiring pattern (lines 88-91)

```csharp
_scrollResizeHandler = new ScrollResizeHandler(_configManager.Settings);
_hookManager.MouseWheeled += _scrollResizeHandler.OnMouseWheeled;
_hookManager.WheelSuppressionPredicate = _scrollResizeHandler.ShouldSuppressWheel;
_scrollResizeHandler.Install();
```

Replace with:

```csharp
_scrollResizeHandler = new ScrollResizeHandler(_configManager.Settings);
_hookManager.MouseWheeled += _scrollResizeHandler.OnMouseWheeled;

_windowTransparencyHandler = new WindowTransparencyHandler(_configManager.Settings);
_hookManager.MouseWheeled += _windowTransparencyHandler.OnMouseWheeled;
_hookManager.WheelSuppressionPredicate = msg =>
    _scrollResizeHandler.ShouldSuppressWheel(msg) ||
    _windowTransparencyHandler.ShouldSuppressWheel(msg);
_scrollResizeHandler.Install();
_windowTransparencyHandler.Install();
```

Key rules:
- `WheelSuppressionPredicate` is a single-slot `Func<int, bool>?` field — NOT an event. Use an OR lambda; do NOT use `+=`.
- `Install()` is called after all hook wiring for both handlers.

---

#### ShowSettingsWindow OK branch — settings save pattern (lines 144-145)

```csharp
_configManager.Settings.ScrollResizeStep = form.SelectedScrollResizeStep;
```

Add immediately after that line:

```csharp
_configManager.Settings.TransparencyStep = form.SelectedTransparencyStep;
```

Pattern: one line per setting, read from the matching `Selected*` public property on `SettingsForm`, assign to `_configManager.Settings.*`.

---

#### DisposeComponents pattern (lines 210-212)

```csharp
_hookManager.MouseWheeled -= _scrollResizeHandler.OnMouseWheeled;
_hookManager.WheelSuppressionPredicate = null;
_scrollResizeHandler.Dispose();
```

Replace with:

```csharp
_hookManager.MouseWheeled -= _scrollResizeHandler.OnMouseWheeled;
_hookManager.MouseWheeled -= _windowTransparencyHandler.OnMouseWheeled;
_hookManager.WheelSuppressionPredicate = null;
_scrollResizeHandler.Dispose();
_windowTransparencyHandler.Dispose();
```

Key rules:
- Unsubscribe both `MouseWheeled` handlers before nulling `WheelSuppressionPredicate`.
- `WheelSuppressionPredicate = null` stays as a single assignment (clearing the combined lambda).
- `Dispose()` called for each handler after the null-out.

---

### `WindowsHotSpot/UI/SettingsForm.cs` (component, request-response)

**Analog:** Same file — `_scrollResizeStepInput` / Window Interactions group

---

#### Public property pattern (line 48)

```csharp
public int SelectedScrollResizeStep => (int)_scrollResizeStepInput.Value;
```

Add immediately after, in the same public API block:

```csharp
public int SelectedTransparencyStep => (int)_transparencyStepInput.Value;
```

---

#### Field declaration pattern (line 65)

```csharp
private readonly NumericUpDown _scrollResizeStepInput;
```

Add immediately after:

```csharp
private readonly NumericUpDown _transparencyStepInput;
```

---

#### Window Interactions group — current code to replace (lines 323-348)

```csharp
var windowInteractionsGroup = new GroupBox
{
    Text = "Window Interactions",
    Location = new Point(12, windowInteractionsGroupTop),
    Size = new Size(396, 48),
};

var scrollStepLabel = MakeLabel("Scroll resize step:", 12, 24);

_scrollResizeStepInput = new NumericUpDown
{
    Minimum   = 1,
    Maximum   = 200,
    Increment = 5,
    Value     = Math.Clamp(settings.ScrollResizeStep, 1, 200),
    Location  = new Point(136, 21),
    Width     = 65,
};

var scrollStepUnitLabel = MakeLabel("px / notch", 209, 24);

windowInteractionsGroup.Controls.AddRange(
    [scrollStepLabel, _scrollResizeStepInput, scrollStepUnitLabel]);
```

Replace with (grow group height 48→78, add second row at y=51, update Controls.AddRange):

```csharp
var windowInteractionsGroup = new GroupBox
{
    Text = "Window Interactions",
    Location = new Point(12, windowInteractionsGroupTop),
    Size = new Size(396, 78),
};

var scrollStepLabel = MakeLabel("Scroll resize step:", 12, 24);

_scrollResizeStepInput = new NumericUpDown
{
    Minimum   = 1,
    Maximum   = 200,
    Increment = 5,
    Value     = Math.Clamp(settings.ScrollResizeStep, 1, 200),
    Location  = new Point(136, 21),
    Width     = 65,
};

var scrollStepUnitLabel = MakeLabel("px / notch", 209, 24);

var transparencyStepLabel = MakeLabel("Transparency step:", 12, 54);

_transparencyStepInput = new NumericUpDown
{
    Minimum   = 1,
    Maximum   = 50,
    Increment = 1,
    Value     = Math.Clamp(settings.TransparencyStep, 1, 50),
    Location  = new Point(136, 51),
    Width     = 65,
};

var transparencyStepUnitLabel = MakeLabel("α / notch", 209, 54);

windowInteractionsGroup.Controls.AddRange(
[
    scrollStepLabel, _scrollResizeStepInput, scrollStepUnitLabel,
    transparencyStepLabel, _transparencyStepInput, transparencyStepUnitLabel,
]);
```

Layout rules (all from D-09, D-10, D-12):
- Label x=12, NumericUpDown x=136 width=65, unit label x=209 — same columns as scroll resize row.
- First row y=21 (NumericUpDown), y=24 (labels via MakeLabel which adds +3 baseline offset).
- Second row y=51 (NumericUpDown), y=54 (labels).
- Row spacing is 30px.

---

#### buttonPanelTop cascade — current line (line 351)

```csharp
int buttonPanelTop = windowInteractionsGroupTop + 56;
```

Replace with (group grew 30px → offset grows 30px):

```csharp
int buttonPanelTop = windowInteractionsGroupTop + 86;
```

`ClientSize` height formula `buttonPanelTop + 44` (line 385) requires no change — it cascades automatically.

---

## Shared Patterns

### MakeLabel helper
**Source:** `WindowsHotSpot/UI/SettingsForm.cs` lines 583-589
**Apply to:** All new label controls in SettingsForm

```csharp
private static Label MakeLabel(string text, int x, int y) => new()
{
    Text = text,
    Location = new Point(x, y + 3), // +3 aligns baseline with adjacent input
    AutoSize = true,
    ForeColor = SystemColors.ControlText,
};
```

Pass the raw y coordinate of the NumericUpDown row; MakeLabel adds +3 internally for baseline alignment. For the transparency row: `MakeLabel("Transparency step:", 12, 54)` and `MakeLabel("α / notch", 209, 54)`.

### Math.Clamp for NumericUpDown initial value
**Source:** `WindowsHotSpot/UI/SettingsForm.cs` lines 249, 260, 340
**Apply to:** `_transparencyStepInput` Value initialisation

```csharp
Value = Math.Clamp(settings.TransparencyStep, 1, 50),
```

Clamp bounds must match the NumericUpDown Minimum/Maximum (1–50 per D-12 / TRNSP-04).

### WheelSuppressionPredicate combination
**Source:** `WindowsHotSpot/HotSpotApplicationContext.cs` — current line 90 (single predicate)
**Apply to:** Constructor wiring block only

`WheelSuppressionPredicate` is `Func<int, bool>?` — a plain field, not an event. Two handlers sharing it requires an explicit OR lambda:

```csharp
_hookManager.WheelSuppressionPredicate = msg =>
    _scrollResizeHandler.ShouldSuppressWheel(msg) ||
    _windowTransparencyHandler.ShouldSuppressWheel(msg);
```

Do NOT use `+=`. Nulling it in dispose clears the entire combined predicate.

---

## No Analog Found

None — both files are being modified and their exact analog lines are in the same files.

---

## Metadata

**Analog search scope:** `WindowsHotSpot/HotSpotApplicationContext.cs`, `WindowsHotSpot/UI/SettingsForm.cs`, `WindowsHotSpot/Core/ScrollResizeHandler.cs`, `WindowsHotSpot/Core/WindowTransparencyHandler.cs`
**Files scanned:** 4
**Pattern extraction date:** 2026-05-06
