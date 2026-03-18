# Phase 4: Custom Shortcut & Settings UI — Research

**Researched:** 2026-03-18
**Domain:** WinForms keyboard capture, VK code storage, SendInput dispatch, UI layout (C# / .NET 10 WinForms)
**Confidence:** HIGH

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| CRNA-05 | User can record a custom keystroke for any corner (click-to-record; Escape cancels) | KeyDown/PreviewKeyDown capture pattern; CustomShortcut storage model; ActionDispatcher extension |
| UI-01 | Settings shows a 2×2 corner layout per monitor for visual corner assignment | TableLayoutPanel 2×2 with per-corner GroupBox; manual layout pattern already used in project |
| UI-02 | Settings shows a monitor selector when more than one monitor is connected | ComboBox populated from Screen.AllScreens; show/hide on count; monitor-switch loads per-monitor config |
</phase_requirements>

---

## Summary

Phase 4 has three interlocking deliverables: a keystroke recorder control, a redesigned 2×2 corner grid UI, and a monitor selector. The core complexity is all in the recorder — WinForms does not natively intercept Tab, Escape, arrow, or Win keys in KeyDown unless you explicitly route them through `PreviewKeyDown`. The Win key (LWin/RWin) is additionally unusual because it arrives as WM_KEYDOWN only when the hook has already consumed it; in a normal WinForms form it is absorbed by the shell before reaching KeyDown. The recommended approach (LOW Windows API overhead) is to capture keys directly in a Panel control using `PreviewKeyDown` + `KeyDown` because a Panel can take focus but does not process Tab/Enter/Escape the same way a TextBox does.

The storage model change is the most architecturally significant decision. `CornerAction` is currently a pure enum; adding `CustomShortcut` as an enum case means the action that gets dispatched is no longer self-contained in the enum value — the actual VK sequence lives elsewhere. The cleanest approach is a small `CustomShortcut` record (ushort[] VirtualKeys, string DisplayText) stored alongside `CornerActions` in `MonitorCornerConfig`, with `CornerAction.Custom` acting as the discriminator. This avoids changing `CornerAction` to a union/class hierarchy and keeps JSON serialisation simple.

`ActionDispatcher.Dispatch()` must grow a `Custom` case that accepts the `CustomShortcut` data and calls a new `SendArbitraryKeys()` helper following the same press-all/release-all-reverse atomic SendInput pattern. The Settings UI redesign is a full replacement of `SettingsForm`: the existing form already has a `// Phase 4 redesigns this form` comment marking it as temporary.

**Primary recommendation:** Implement keystroke recording with a Panel subclass using PreviewKeyDown for navigation-key interception; store shortcuts as `ushort[]` in a new `CustomShortcut` record; dispatch via an extended `ActionDispatcher` using the existing `MakeKeyInput` pattern.

---

## Standard Stack

### Core

| Component | Version | Purpose | Why Standard |
|-----------|---------|---------|--------------|
| System.Windows.Forms (WinForms) | .NET 10 | All UI controls | Already the project's UI framework |
| Win32 SendInput / KEYBDINPUT | N/A (P/Invoke) | Synthesize arbitrary keystrokes | Already wired in NativeMethods.cs |
| System.Text.Json | .NET 10 | Persist CustomShortcut to settings.json | Already used for all other settings |

### Supporting

| Component | Version | Purpose | When to Use |
|-----------|---------|---------|-------------|
| Keys enum (System.Windows.Forms) | .NET 10 | Map WinForms key codes to displayable names and VK values | In recorder — translate KeyEventArgs.KeyCode to ushort VK |
| Control.ModifierKeys | .NET 10 | Read active modifier state during KeyDown | In recorder — supplement KeyEventArgs.Modifiers |
| TableLayoutPanel | .NET 10 | 2×2 grid layout for corner controls | UI-01 corner grid |
| ComboBox | .NET 10 | Monitor selector | UI-02 monitor switcher |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Panel subclass for recorder | TextBox with ReadOnly=true | TextBox intercepts Backspace, Enter, Tab as editing actions; Panel is cleaner for pure key capture |
| ushort[] VirtualKeys in CustomShortcut | Keys enum storage | Keys is WinForms-specific and includes modifier-flag bits mixed in; ushort[] maps 1:1 to KEYBDINPUT.wVk with no masking needed |
| ComboBox for monitor selector | TabControl per monitor | ComboBox is simpler for 1-N monitors; Tab pages add visual complexity for 2-4 monitors |

**Installation:** No new NuGet packages needed. All components are in `System.Windows.Forms` (.NET 10) and the existing P/Invoke declarations.

---

## Architecture Patterns

### Recommended Project Structure

No new files/folders needed beyond what Phase 3 established. New and changed files:

```
WindowsHotSpot/
├── Config/
│   └── AppSettings.cs          # Add CustomShortcut record; add CustomShortcuts dict to MonitorCornerConfig
├── Core/
│   └── ActionDispatcher.cs     # Add Custom case; add SendArbitraryKeys helper
├── UI/
│   ├── SettingsForm.cs         # Full redesign: 2×2 grid + monitor selector + Record buttons
│   └── KeyRecorderPanel.cs     # NEW: focusable Panel subclass for keystroke capture
```

### Pattern 1: KeyRecorderPanel — Focusable Panel With Key Interception

**What:** A Panel subclass that accepts focus and captures all keystrokes including Tab, Escape, and arrow keys. Enters "recording mode" when activated; exits on Escape (cancel) or any non-modifier key (commit).

**When to use:** Anywhere a "press a key" capture is needed. The panel sets `e.IsInputKey = true` for all keys in PreviewKeyDown so that KeyDown receives them.

**Example:**

```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.control.previewkeydown
// + https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.control.keydown
internal sealed class KeyRecorderPanel : Panel
{
    // Raised when user completes a recording (non-modifier key pressed)
    public event Action<ushort[]>? ShortcutRecorded;
    // Raised when user cancels (Escape pressed alone)
    public event Action? RecordingCancelled;

    private bool _isRecording;

    public KeyRecorderPanel()
    {
        // Panel must be able to receive focus
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
    }

    public void StartRecording()
    {
        _isRecording = true;
        Text = "Press a key…";
        Focus();
    }

    protected override void OnPreviewKeyDown(PreviewKeyDownEventArgs e)
    {
        // Mark ALL keys as input keys so KeyDown receives them, including
        // Tab (0x09), Escape (0x1B), arrow keys, and Enter.
        // Win key (LWin/RWin) does NOT arrive here in a normal form context
        // (shell absorbs it), so no special handling is needed.
        if (_isRecording)
            e.IsInputKey = true;
        base.OnPreviewKeyDown(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!_isRecording)
        {
            base.OnKeyDown(e);
            return;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;

        // Escape alone = cancel recording
        if (e.KeyCode == Keys.Escape && e.Modifiers == Keys.None)
        {
            _isRecording = false;
            RecordingCancelled?.Invoke();
            return;
        }

        // Pure modifier keys alone (Shift, Ctrl, Alt) — not a valid shortcut yet, keep waiting
        if (IsModifierOnly(e.KeyCode))
            return;

        // Build VK sequence: modifiers first (in press order), then the main key
        var vks = BuildVkSequence(e);
        _isRecording = false;
        ShortcutRecorded?.Invoke(vks);
    }

    private static bool IsModifierOnly(Keys key) => key is
        Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey or
        Keys.ControlKey or Keys.LControlKey or Keys.RControlKey or
        Keys.Menu or Keys.LMenu or Keys.RMenu;

    private static ushort[] BuildVkSequence(KeyEventArgs e)
    {
        // Collect modifiers present, then append the main key VK.
        // Stored as ushort[] so they map 1:1 to KEYBDINPUT.wVk in SendInput.
        var vks = new List<ushort>();
        if ((e.Modifiers & Keys.Control) != 0) vks.Add(0xA2); // VK_LCONTROL
        if ((e.Modifiers & Keys.Shift)   != 0) vks.Add(0xA0); // VK_LSHIFT
        if ((e.Modifiers & Keys.Alt)     != 0) vks.Add(0xA4); // VK_LMENU
        vks.Add((ushort)e.KeyCode);
        return [.. vks];
    }
}
```

### Pattern 2: CustomShortcut Record and Storage

**What:** A small record holding the VK sequence and a display-ready label. Stored as a new `CustomShortcuts` dictionary in `MonitorCornerConfig` (parallel to `CornerActions`). `CornerAction.Custom` is a new enum value that signals "look up this corner in CustomShortcuts".

**Example:**

```csharp
// In AppSettings.cs — add after CornerAction enum

internal sealed record CustomShortcut(ushort[] VirtualKeys, string DisplayText)
{
    // Parameterless constructor required by System.Text.Json
    public CustomShortcut() : this([], string.Empty) { }
}

// In CornerAction enum — add new value:
// [JsonConverter(typeof(JsonStringEnumConverter))]
// internal enum CornerAction { Disabled, TaskView, ShowDesktop, ActionCenter, Custom }

// In MonitorCornerConfig — add alongside CornerActions:
// public Dictionary<HotCorner, CustomShortcut> CustomShortcuts { get; set; } = new();
```

**JSON representation (settings.json after recording Ctrl+F5 on TopLeft of monitor 1):**
```json
"MonitorConfigs": {
  "\\\\.\\DISPLAY1": {
    "CornerActions": { "TopLeft": "Custom", ... },
    "CustomShortcuts": {
      "TopLeft": { "VirtualKeys": [162, 116], "DisplayText": "Ctrl+F5" }
    }
  }
}
```

### Pattern 3: ActionDispatcher Extension for Custom Shortcuts

**What:** Add a `Custom` overload to `ActionDispatcher.Dispatch()` that accepts the `CustomShortcut` and calls `SendArbitraryKeys()`. The press/release sequence is: press all VKs in order, then release all in reverse order (same atomic SendInput call).

**Example:**

```csharp
// In ActionDispatcher.cs
// Called from CornerDetector.OnDwellComplete (already on UI thread via WinForms Timer)

public static void Dispatch(CornerAction action, CustomShortcut? custom = null)
{
    switch (action)
    {
        case CornerAction.TaskView:    ActionTrigger.SendTaskView(); break;
        case CornerAction.ShowDesktop: SendWinKey(NativeMethods.VK_D); break;
        case CornerAction.ActionCenter: SendWinKey(NativeMethods.VK_A); break;
        case CornerAction.Custom:
            if (custom is not null)
                SendArbitraryKeys(custom.VirtualKeys);
            break;
        case CornerAction.Disabled:    break;
    }
}

// Press all keys down in order, then release all in reverse order.
// All structs sent in one atomic SendInput call — consistent with existing 4-struct pattern.
private static void SendArbitraryKeys(ushort[] vks)
{
    if (vks.Length == 0) return;
    var inputs = new NativeMethods.INPUT[vks.Length * 2];
    for (int i = 0; i < vks.Length; i++)
        inputs[i] = MakeKeyInput(vks[i], keyUp: false);
    for (int i = 0; i < vks.Length; i++)
        inputs[vks.Length + (vks.Length - 1 - i)] = MakeKeyInput(vks[i], keyUp: true);
    NativeMethods.SendInput(
        (uint)inputs.Length,
        inputs,
        Marshal.SizeOf<NativeMethods.INPUT>()); // MUST be Marshal.SizeOf — see MEMORY.md
}
```

### Pattern 4: CornerDetector Parameterisation for Custom

**What:** `CornerDetector` currently stores a `CornerAction _action`. For `Custom`, it also needs the `CustomShortcut` data. The simplest approach: add a nullable `CustomShortcut? _customShortcut` field; pass it through the constructor. `OnDwellComplete` passes both to `Dispatch`.

**Example:**

```csharp
// CornerDetector constructor change:
public CornerDetector(HotCorner corner, Rectangle screenBounds,
    int zoneSize, int dwellDelay, CornerAction action,
    CustomShortcut? customShortcut = null)
{
    // ... existing assignments ...
    _action = action;
    _customShortcut = customShortcut;
}

// OnDwellComplete change:
private void OnDwellComplete(object? sender, EventArgs e)
{
    _dwellTimer.Stop();
    ActionDispatcher.Dispatch(_action, _customShortcut);
    _state = DetectorState.Triggered;
}
```

### Pattern 5: CornerRouter Rebuild for Custom

**What:** `CornerRouter.Rebuild()` already iterates `CornerActions`. For `Custom` corners, it must also look up the matching `CustomShortcut` from `MonitorCornerConfig.CustomShortcuts` and pass it to the new `CornerDetector` constructor parameter.

**Example (relevant loop change):**

```csharp
foreach (var (corner, action) in actions)
{
    if (action == CornerAction.Disabled) continue;
    CustomShortcut? custom = null;
    if (action == CornerAction.Custom)
        monitorConfig?.CustomShortcuts.TryGetValue(corner, out custom);
    detectors.Add(new CornerDetector(
        corner, screen.Bounds, settings.ZoneSize, settings.DwellDelayMs,
        action, custom));
}
```

### Pattern 6: 2×2 Corner Grid UI (TableLayoutPanel)

**What:** Replace the old single-corner ComboBox with a `TableLayoutPanel` (2 cols × 2 rows). Each cell contains a `GroupBox` named for the corner ("Top Left", etc.) with an action ComboBox and a "Record" button. Pressing Record activates the `KeyRecorderPanel` for that corner.

**When to use:** Whenever per-corner configuration is shown for one monitor. The grid is rebuilt or repopulated when the monitor selector changes.

**Layout:**

```
┌─────────────────────────────────────────┐
│  ┌──── Top Left ────┐  ┌── Top Right ──┐│
│  │ [Action ▼] [Rec] │  │[Action▼][Rec] ││
│  └──────────────────┘  └───────────────┘│
│  ┌── Bottom Left ───┐  ┌─ Bottom Right ┐│
│  │ [Action ▼] [Rec] │  │[Action▼][Rec] ││
│  └──────────────────┘  └───────────────┘│
└─────────────────────────────────────────┘
```

Grid sizing: each cell ~160×70px, grid 340×160px total, group form ClientSize to ~380×340 to accommodate detection params + system group + buttons.

### Anti-Patterns to Avoid

- **Storing Keys enum values directly in settings.json:** `Keys` values include modifier-flag bits (0x10000 for Shift etc.) mixed into a 32-bit int. They do not round-trip cleanly as ushort VK codes and will break SendInput.
- **Using a TextBox for key recording:** TextBox processes Backspace as deletion, Tab as focus-move, Enter as AcceptButton trigger. These intercept the very keys users may want to record.
- **Calling `Screen.AllScreens` in the monitor selector's Paint or layout code:** Expensive GDI enumeration. Call once when SettingsForm opens, store in a local variable.
- **Using VK_SHIFT/VK_CONTROL/VK_MENU (non-side-specific VKs) in SendInput:** These are legal but some applications distinguish left vs right modifier. Store and send VK_LSHIFT (0xA0), VK_LCONTROL (0xA2), VK_LMENU (0xA4) for consistency — matches the pattern already used in ActionDispatcher.
- **Single CornerAction enum value "Custom" without CustomShortcut backing data:** If the backing data is missing (null), Dispatch must no-op silently rather than throw. Always guard with `if (custom is not null)`.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Key name display string | Custom VK→name lookup table | `new KeysConverter().ConvertToString((Keys)vk)` | WinForms `KeysConverter` handles all VK→display-name conversions including F-keys, OEM keys, numpad |
| Modifier state at KeyDown time | Manual GetAsyncKeyState calls | `KeyEventArgs.Modifiers` (already contains active modifier flags) | Modifiers property is populated by WinForms from the message; correct for this synchronous use |
| Monitor display name | EDID / SetupAPI queries | `"Display " + (index+1) + (screen.Primary ? " (Primary)" : "")` | Out of scope per REQUIREMENTS.md; simple index label is sufficient |

**Key insight:** `KeysConverter.ConvertToString` in WinForms produces human-readable labels ("Ctrl+F5", "Alt+Home") automatically from a combined `Keys` value. Build a `Keys` value for display purposes only; store raw `ushort[]` for dispatch.

---

## Common Pitfalls

### Pitfall 1: Win Key (LWin/RWin) Does Not Arrive at KeyDown in Normal Forms

**What goes wrong:** User presses Win+F (wanting to record a Win-key shortcut). `KeyDown` never fires with `Keys.LWin`. The Win key is intercepted by the Windows shell before it reaches WM_KEYDOWN in a standard window.

**Why it happens:** Windows uses a low-level keyboard hook internally to intercept Win key presses for the Start menu and system shortcuts. A normal window's WM_KEYDOWN never sees VK_LWIN/VK_RWIN unless a WH_KEYBOARD_LL hook (like our own HookManager) has already blocked the shell's consumption — but our hook runs in a different callback and does not block.

**How to avoid:** Do not support Win-key combinations as recordable shortcuts. Display a note in the recorder UI: "Win key combinations are not recordable." The built-in Win+Tab, Win+D, Win+A actions cover the primary Win-key use cases.

**Warning signs:** User presses Win+letter, nothing is recorded, and the Start menu opens instead.

### Pitfall 2: Escape Must Cancel Without Triggering DialogResult.Cancel

**What goes wrong:** The SettingsForm has `CancelButton = _cancelButton` which maps Escape to `DialogResult.Cancel`. If Escape fires while a `KeyRecorderPanel` is in recording mode, it would both cancel the recording AND dismiss the form.

**Why it happens:** WinForms routes Escape to the form's `CancelButton` at the form level via `ProcessDialogKey`, which fires before `KeyDown` on the focused control. `PreviewKeyDown + e.IsInputKey = true` does NOT suppress `ProcessDialogKey` — it only ensures `KeyDown` fires; the form's dialog-key routing still intercepts Escape first.

**How to avoid:** Override `ProcessCmdKey` on the form to suppress Escape forwarding to the CancelButton when any `KeyRecorderPanel` is in recording mode:

```csharp
// In SettingsForm — add field: private bool _anyPanelRecording;
// Set _anyPanelRecording = true/false in recorder panel event handlers.

protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
{
    // While a recorder panel is active, swallow Escape at form level
    // so it reaches the panel's KeyDown instead of triggering Cancel.
    if (_anyPanelRecording && keyData == Keys.Escape)
        return false;   // not handled here — let it propagate to focused control
    return base.ProcessCmdKey(ref msg, keyData);
}
```

**Warning signs:** Pressing Escape during recording dismisses the Settings form entirely.

### Pitfall 3: `CornerAction` Enum Serialisation Is String-Based

**What goes wrong:** Adding `Custom` to the `CornerAction` enum is safe for new files, but existing settings.json files with integer-serialised values (if any legacy path exists) would break. Also: removing an enum member later would orphan stored "Custom" strings.

**Why it happens:** The enum uses `[JsonConverter(typeof(JsonStringEnumConverter))]`, so values serialise as their name strings ("Custom"), not integers. This is correct and forward-compatible.

**How to avoid:** Adding "Custom" to the enum is fully backward-compatible: existing settings.json files that don't contain "Custom" will not be affected. New "Custom" entries round-trip correctly. No migration needed.

**Warning signs:** None for the addition; only a concern if an enum value is later renamed.

### Pitfall 4: cbSize Bug (Already Known — Must Not Regress)

**What goes wrong:** `SendArbitraryKeys` (new helper) must use `Marshal.SizeOf<NativeMethods.INPUT>()` for cbSize, not a hardcoded value.

**Why it happens:** See MEMORY.md "Critical Bug Fixed" — `InputUnion` includes `MOUSEINPUT` to pad to 40 bytes. Any new code that calls SendInput must use the same pattern.

**How to avoid:** The existing `MakeKeyInput` helper in `ActionDispatcher` already does this correctly. `SendArbitraryKeys` reuses it.

**Warning signs:** `SendInput` returns 0 (the new shortcut fires silently and nothing happens).

### Pitfall 5: `CustomShortcut` JSON Serialisation — ushort[] Round-Trip

**What goes wrong:** `System.Text.Json` serialises `ushort[]` as a JSON number array (e.g. `[162, 116]`) and deserialises it correctly. However, if the property is later changed to `List<ushort>` the round-trip still works. Avoid `object[]` or dynamic types.

**Why it happens:** Type mismatch between serialised and deserialised types causes `JsonException` on load.

**How to avoid:** Keep `VirtualKeys` typed as `ushort[]` throughout. Do not use `JsonElement` or untyped arrays.

**Warning signs:** Settings load throws `JsonException: ...cannot be converted to ushort`.

### Pitfall 6: Monitor Selector Ordering Must Be Stable

**What goes wrong:** `Screen.AllScreens` returns monitors in an unspecified order that can change when monitors are connected/disconnected. If the ComboBox is indexed by position rather than `Screen.DeviceName`, the user's selection silently points at the wrong monitor after a display change.

**Why it happens:** `AllScreens` order is not guaranteed by the Windows API.

**How to avoid:** Store the selected `Screen.DeviceName` string (not the index) as the binding key. When repopulating the monitor selector, select by DeviceName match.

**Warning signs:** Monitor selector shows wrong settings after unplugging and re-plugging a monitor.

---

## Code Examples

### Capture key combination from Panel — setting IsInputKey for navigation keys

```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.control.previewkeydown
// Pattern: set e.IsInputKey = true in PreviewKeyDown; handle in KeyDown
protected override void OnPreviewKeyDown(PreviewKeyDownEventArgs e)
{
    if (_isRecording)
        e.IsInputKey = true;   // forces KeyDown to fire for Tab, Esc, arrows, Enter
    base.OnPreviewKeyDown(e);
}
```

### Convert Keys value to display string

```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.keysconverter
// KeysConverter handles all VK→name mappings including modifier combinations
var converter = new KeysConverter();
string label = converter.ConvertToString(e.KeyData) ?? e.KeyData.ToString();
// e.g. Keys.Control | Keys.F5  →  "Ctrl+F5"
// e.g. Keys.Alt | Keys.Home    →  "Alt+Home"
```

### Populate monitor selector from Screen.AllScreens

```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.screen.allscreens
// Call once on form open; bind ComboBox by DeviceName
var screens = Screen.AllScreens;
_monitorCombo.Items.Clear();
for (int i = 0; i < screens.Length; i++)
{
    var label = $"Display {i + 1}{(screens[i].Primary ? " (Primary)" : "")}";
    _monitorCombo.Items.Add((Label: label, DeviceName: screens[i].DeviceName));
}
_monitorCombo.Visible = screens.Length > 1;   // UI-02: only show if >1 monitor
```

### Suppress Escape at form level when recorder is active

```csharp
// Source: https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.control.processcmdkey
protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
{
    if (_anyPanelRecording && keyData == Keys.Escape)
        return false;  // let Escape reach the focused KeyRecorderPanel
    return base.ProcessCmdKey(ref msg, keyData);
}
```

### Atomic SendInput for arbitrary VK sequence (press-all then release-all-reverse)

```csharp
// Source: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput
// Same pattern as ActionDispatcher.SendWinKey / ActionTrigger.SendTaskView
private static void SendArbitraryKeys(ushort[] vks)
{
    if (vks.Length == 0) return;
    var inputs = new NativeMethods.INPUT[vks.Length * 2];
    for (int i = 0; i < vks.Length; i++)
        inputs[i] = MakeKeyInput(vks[i], keyUp: false);
    for (int i = 0; i < vks.Length; i++)
        inputs[vks.Length + (vks.Length - 1 - i)] = MakeKeyInput(vks[i], keyUp: true);
    NativeMethods.SendInput(
        (uint)inputs.Length, inputs,
        Marshal.SizeOf<NativeMethods.INPUT>());
}
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Single `Corner` property in AppSettings (Phase 1) | `CornerActions` dictionary per monitor (`MonitorCornerConfig`) | Phase 2/3 | Phase 4 extends `MonitorCornerConfig` with `CustomShortcuts` dictionary |
| Single CornerDetector | CornerRouter pool of detectors per (monitor, corner) | Phase 3 | Phase 4 adds `CustomShortcut?` parameter to CornerDetector |
| SettingsForm single-corner ComboBox (placeholder since Phase 2) | Full 2×2 grid per monitor with Record buttons | Phase 4 (this phase) | Complete replacement of SettingsForm |

**Deprecated/outdated:**
- The current `SettingsForm._cornerCombo` + its `SelectedCorner` property: replaced by the 2×2 grid. The comment `// Phase 4 redesigns this form` in SettingsForm.cs confirms this is intentional.
- `HotSpotApplicationContext.ShowSettingsWindow()` lines that write `form.SelectedZoneSize` etc: these will be updated to read from the redesigned form's data model.

---

## Open Questions

1. **Should single-key-no-modifier shortcuts be allowed?**
   - What we know: CRNA-05 says "keystroke" without specifying modifier requirement. A single key like F9 is a valid shortcut.
   - What's unclear: Single keys like letters (e.g. pressing just "A") would fire every time the corner is dwelled, which could interfere with typing. There is no system to detect "is the user currently typing in another app".
   - Recommendation: Allow single keys (including F-keys, numpad, etc.) but exclude bare letter/number keys. Require at least one modifier if the key is alphanumeric (0-9, A-Z). Display validation message: "Add Ctrl, Shift, or Alt for letter/number keys."

2. **DisplayText generation: who builds it?**
   - What we know: `KeysConverter.ConvertToString` can produce the display string from a `Keys` value. This happens at record time in the UI.
   - What's unclear: Should `DisplayText` be regenerated from `VirtualKeys` on load, or stored and loaded from settings.json?
   - Recommendation: Store in settings.json. Regeneration from raw VKs requires a VK→Keys mapping and re-running KeysConverter, which is overkill for a display label. Stored string is simpler and human-readable in the JSON file.

3. **What happens to GlobalCornerActions (settings.CornerActions) in the redesigned UI?**
   - What we know: Phase 3 CornerRouter uses `settings.CornerActions` as fallback when no `MonitorCornerConfig` exists for a screen.
   - What's unclear: Should the Phase 4 UI expose global corner assignments at all, or only per-monitor ones?
   - Recommendation: The Phase 4 UI should only expose per-monitor configuration for each connected monitor (creating a `MonitorCornerConfig` if one doesn't exist). The global `CornerActions` fallback remains for compatibility with Phase 2 migrated data and disconnected-monitor retention (MMON-04), but the UI need not surface it directly. This simplifies the form.

---

## Sources

### Primary (HIGH confidence)

- https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes — Full VK code table including VK_LWIN (0x5B), VK_LCONTROL (0xA2), VK_LSHIFT (0xA0), VK_LMENU (0xA4); verified 2025-10-06
- https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput — SendInput signature, cbSize requirement, atomicity guarantee; verified 2018-12-05 (stable API)
- https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.control.keydown — KeyDown event, KeyEventArgs.KeyCode + Modifiers, PreviewKeyDown + IsInputKey pattern; updated 2026-03-13
- https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.control.previewkeydown — PreviewKeyDown IsInputKey interception for Tab, Esc, arrows; updated 2026-03-13
- https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.keys — Keys enum values, Modifiers bitmask (-65536 = 0xFFFF0000); updated 2026-02-11
- Codebase: `WindowsHotSpot/Config/AppSettings.cs`, `Core/ActionDispatcher.cs`, `Core/CornerDetector.cs`, `Core/CornerRouter.cs`, `UI/SettingsForm.cs`, `Native/NativeMethods.cs` — read directly

### Secondary (MEDIUM confidence)

- STATE.md concern entry: "Hotkey recorder edge cases (Win key as modifier, single-key-no-modifier, Ctrl+Alt+Del rejection) need design decisions" — confirms Win key limitation is known

### Tertiary (LOW confidence)

- Win key absorption by shell (WH_KEYBOARD_LL): based on known Windows architecture; not directly linked to a doc page but consistent with the project's own HookManager using WH_MOUSE_LL approach and with PowerToys Keyboard Manager behaviour.

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — WinForms .NET 10, SendInput, System.Text.Json all verified in official docs and match existing codebase
- Architecture patterns: HIGH — PreviewKeyDown/KeyDown pattern verified in official docs; CustomShortcut storage model derived from existing codebase patterns
- Pitfalls: HIGH (Pitfall 1 Win key, Pitfall 2 Escape/CancelButton verified with official docs); MEDIUM (Pitfall 6 AllScreens ordering — known GDI behaviour, same concern flagged in STATE.md for Phase 3)

**Research date:** 2026-03-18
**Valid until:** 2026-04-18 (stable APIs — .NET 10 WinForms, Win32 SendInput)
