# Phase 2: Settings and Configuration - Research

**Researched:** 2026-03-11
**Domain:** WinForms settings dialog, System.Text.Json persistence, Windows Registry startup key
**Confidence:** HIGH

## Summary

Phase 2 extracts the hard-coded values in `CornerDetector` (corner=TopLeft, zoneSize=10, dwellDelay=300) into a user-configurable settings system. This involves four new components: `AppSettings` (POCO model), `ConfigManager` (JSON load/save with change notification), `SettingsForm` (WinForms dialog), and `StartupManager` (HKCU Run key). All technologies are inbox .NET -- no NuGet packages needed.

The primary integration challenge is wiring settings changes back to the live `CornerDetector` instance without restarting the app. Phase 1 created `CornerDetector` with `readonly` fields for corner, zone size, and dwell delay. These must become mutable properties (or the detector must expose an `UpdateSettings` method) so that saving from the SettingsForm can take effect immediately. Since both the settings dialog and the hook callback run on the same UI thread (single STA architecture from Phase 1), there are no threading concerns -- direct property updates are safe.

The secondary concern is the `StartupManager` registry path. `Environment.ProcessPath` (available since .NET 6) returns the correct exe path even for single-file published apps, unlike `Assembly.Location` which returns empty string. The registry value must be quoted to handle paths with spaces.

**Primary recommendation:** Create ConfigManager as a simple class (not singleton) owned by HotSpotApplicationContext, pass AppSettings to CornerDetector via an UpdateSettings method, and show SettingsForm as a modal dialog (safe because the hook callback returns fast regardless of whether the UI thread is pumping modal messages).

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| CONF-01 | User can configure which corner is active (TL/TR/BL/BR) | AppSettings.Corner enum + SettingsForm ComboBox; CornerDetector already has HotCorner enum and GetCornerPoint |
| CONF-02 | User can configure zone size in pixels (default 10px) | AppSettings.ZoneSize int + NumericUpDown; CornerDetector._zoneSize becomes configurable |
| CONF-03 | User can configure dwell delay in ms (default 300ms) | AppSettings.DwellDelayMs int + NumericUpDown; CornerDetector._dwellDelay and timer interval updated |
| CONF-04 | Settings persisted to JSON file next to exe, loaded on startup | ConfigManager with System.Text.Json, path = AppContext.BaseDirectory + "settings.json" |
| CONF-05 | User can enable/disable Start with Windows (HKCU Run key) | StartupManager reads/writes Registry.CurrentUser Run key |
| SETT-01 | Settings dialog: change active corner (dropdown) | SettingsForm with ComboBox bound to HotCorner enum values |
| SETT-02 | Settings dialog: change zone size (numeric) | SettingsForm with NumericUpDown, min=1, max=50 |
| SETT-03 | Settings dialog: change dwell delay (numeric) | SettingsForm with NumericUpDown, min=50, max=2000 |
| SETT-04 | Settings dialog: Start with Windows checkbox | SettingsForm with CheckBox, reads/writes via StartupManager |
| SETT-05 | Settings changes take effect immediately on save | ConfigManager.SettingsChanged event -> CornerDetector.UpdateSettings() |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Text.Json | inbox (.NET 10) | Serialize/deserialize AppSettings to JSON | Zero-dependency, high-performance, supports POCO with property names |
| Microsoft.Win32.Registry | inbox (.NET 10) | Read/write HKCU Run key for startup | The only managed API for Windows Registry access |
| System.Windows.Forms | inbox (.NET 10) | SettingsForm dialog UI | Already used by Phase 1 for tray icon and timer |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| System.Text.Json.JsonSerializerOptions | inbox | WriteIndented=true for human-readable settings file | Always -- users may hand-edit settings.json |
| System.Text.Json.Serialization.JsonStringEnumConverter | inbox | Serialize HotCorner enum as string not int | Always -- "TopLeft" is more readable than "0" in JSON |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| System.Text.Json | Newtonsoft.Json | Would add NuGet dependency; System.Text.Json is sufficient for this simple POCO |
| JSON file next to exe | %AppData% folder | AppData is standard for installed apps but exe-adjacent is simpler for a portable utility; matches architecture decision |
| Modal SettingsForm | Modeless form | Modal is simpler, prevents multiple instances, and the hook continues working because WM_MOUSEMOVE is dispatched even during modal loops |

**Installation:** No packages to install. All APIs are inbox in .NET 10 with `<UseWindowsForms>true</UseWindowsForms>`.

## Architecture Patterns

### Recommended Project Structure
```
WindowsHotSpot/
  Config/
    AppSettings.cs        # Settings POCO + HotCorner enum (move from Core/)
    ConfigManager.cs      # JSON load/save + SettingsChanged event
    StartupManager.cs     # HKCU Run key read/write
  UI/
    SettingsForm.cs       # WinForms dialog (manual layout, no designer)
  Core/
    CornerDetector.cs     # Modified: accept settings, expose UpdateSettings()
    HookManager.cs        # Unchanged
    ActionTrigger.cs      # Unchanged
```

### Pattern 1: AppSettings POCO with JSON Serialization
**What:** A plain C# class with auto-properties and sensible defaults, serialized with System.Text.Json using JsonStringEnumConverter for readable enum values.
**When to use:** Always for the settings model.
**Example:**
```csharp
// Config/AppSettings.cs
using System.Text.Json.Serialization;

namespace WindowsHotSpot.Config;

internal sealed class AppSettings
{
    public HotCorner Corner { get; set; } = HotCorner.TopLeft;
    public int ZoneSize { get; set; } = 10;
    public int DwellDelayMs { get; set; } = 300;
    public bool StartWithWindows { get; set; } = false;
}

// HotCorner enum moves here from Core/CornerDetector.cs
[JsonConverter(typeof(JsonStringEnumConverter))]
internal enum HotCorner { TopLeft, TopRight, BottomLeft, BottomRight }
```

### Pattern 2: ConfigManager with Change Notification
**What:** A class that owns load/save of AppSettings and fires a SettingsChanged event so consumers can react.
**When to use:** Owned by HotSpotApplicationContext, passed to SettingsForm and used by CornerDetector.
**Example:**
```csharp
// Config/ConfigManager.cs
using System.Text.Json;

namespace WindowsHotSpot.Config;

internal sealed class ConfigManager
{
    private static readonly string SettingsPath =
        Path.Combine(AppContext.BaseDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
    };

    public AppSettings Settings { get; private set; } = new();

    public event Action? SettingsChanged;

    public void Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                Settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
        }
        catch
        {
            // Corrupt file: use defaults, will be overwritten on next save
            Settings = new AppSettings();
        }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(Settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
        SettingsChanged?.Invoke();
    }
}
```

### Pattern 3: CornerDetector Settings Update (SETT-05)
**What:** CornerDetector exposes an UpdateSettings method that replaces its internal configuration values. Called after ConfigManager.Save().
**When to use:** When settings change and must take effect immediately without restarting.
**Example:**
```csharp
// Modifications to Core/CornerDetector.cs
internal sealed class CornerDetector : IDisposable
{
    private HotCorner _activeCorner;
    private int _zoneSize;
    private int _dwellDelay;

    public CornerDetector(HotCorner corner, int zoneSize, int dwellDelay)
    {
        _activeCorner = corner;
        _zoneSize = zoneSize;
        _dwellDelay = dwellDelay;
        _dwellTimer = new System.Windows.Forms.Timer { Interval = _dwellDelay };
        _dwellTimer.Tick += OnDwellComplete;
    }

    public void UpdateSettings(HotCorner corner, int zoneSize, int dwellDelay)
    {
        _activeCorner = corner;
        _zoneSize = zoneSize;
        _dwellDelay = dwellDelay;
        _dwellTimer.Interval = dwellDelay;
        // Reset state to prevent stale trigger
        _dwellTimer.Stop();
        _state = DetectorState.Idle;
    }
}
```

### Pattern 4: SettingsForm as Modal Dialog
**What:** A WinForms Form shown with ShowDialog() from the tray menu handler. Contains ComboBox, NumericUpDown controls, CheckBox, and Save/Cancel buttons.
**When to use:** When user clicks "Settings" in the tray context menu.
**Key detail:** Use `AutoScaleMode = AutoScaleMode.Dpi` to prevent blurry controls on high-DPI displays. Use manual layout (no designer files) to keep code simple and diffable.
**Example:**
```csharp
// UI/SettingsForm.cs
namespace WindowsHotSpot.UI;

internal sealed class SettingsForm : Form
{
    private readonly ComboBox _cornerCombo;
    private readonly NumericUpDown _zoneSizeInput;
    private readonly NumericUpDown _dwellDelayInput;
    private readonly CheckBox _startupCheckBox;

    public HotCorner SelectedCorner => (HotCorner)_cornerCombo.SelectedItem!;
    public int SelectedZoneSize => (int)_zoneSizeInput.Value;
    public int SelectedDwellDelay => (int)_dwellDelayInput.Value;
    public bool SelectedStartWithWindows => _startupCheckBox.Checked;

    public SettingsForm(AppSettings settings)
    {
        Text = "WindowsHotSpot Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        // ... create controls, populate from settings, add Save/Cancel buttons
    }
}
```

### Pattern 5: StartupManager for HKCU Run Key
**What:** Static helper that reads/writes the HKCU\Software\Microsoft\Windows\CurrentVersion\Run registry key.
**Critical:** Use `Environment.ProcessPath` (not `Assembly.Location`) for the exe path. Assembly.Location returns empty string for single-file published apps.
**Example:**
```csharp
// Config/StartupManager.cs
using Microsoft.Win32;

namespace WindowsHotSpot.Config;

internal static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "WindowsHotSpot";

    public static bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKey, false);
            return key?.GetValue(AppName) != null;
        }
    }

    public static void SetEnabled(bool enable)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, true)!;
        if (enable)
        {
            // Quote path for spaces; Environment.ProcessPath works in single-file publish
            key.SetValue(AppName, $"\"{Environment.ProcessPath}\"");
        }
        else
        {
            key.DeleteValue(AppName, throwOnMissingValue: false);
        }
    }
}
```

### Pattern 6: Wiring in HotSpotApplicationContext
**What:** HotSpotApplicationContext creates ConfigManager, loads settings, passes them to CornerDetector constructor, subscribes to SettingsChanged event, and replaces the placeholder Settings menu handler with real SettingsForm.
**Example:**
```csharp
// In HotSpotApplicationContext constructor:
_configManager = new ConfigManager();
_configManager.Load();

_cornerDetector = new CornerDetector(
    _configManager.Settings.Corner,
    _configManager.Settings.ZoneSize,
    _configManager.Settings.DwellDelayMs);

_configManager.SettingsChanged += () =>
{
    _cornerDetector.UpdateSettings(
        _configManager.Settings.Corner,
        _configManager.Settings.ZoneSize,
        _configManager.Settings.DwellDelayMs);
};

// Settings menu handler:
private void OnSettingsClick(object? sender, EventArgs e)
{
    using var form = new SettingsForm(_configManager.Settings);
    if (form.ShowDialog() == DialogResult.OK)
    {
        _configManager.Settings.Corner = form.SelectedCorner;
        _configManager.Settings.ZoneSize = form.SelectedZoneSize;
        _configManager.Settings.DwellDelayMs = form.SelectedDwellDelay;
        StartupManager.SetEnabled(form.SelectedStartWithWindows);
        _configManager.Save();
    }
}
```

### Anti-Patterns to Avoid
- **Using Assembly.Location for exe path:** Returns empty string in single-file publish. Use `Environment.ProcessPath` or `AppContext.BaseDirectory`.
- **Making ConfigManager a singleton:** Unnecessary complexity. HotSpotApplicationContext owns it as a field; pass it or its settings where needed.
- **Using System.Timers.Timer for dwell updates:** After changing _dwellTimer.Interval, the timer continues on the UI thread. Do NOT switch timer types.
- **Showing SettingsForm as modeless (Show() instead of ShowDialog()):** Allows multiple settings windows to open. Use ShowDialog() for modal behavior.
- **Not resetting CornerDetector state on settings change:** If the user changes the active corner while dwelling, the old dwell timer could fire. Always stop the timer and reset to Idle in UpdateSettings().

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JSON serialization | Custom string parsing/writing | System.Text.Json.JsonSerializer | Handles escaping, encoding, null values, nested objects |
| Enum serialization | Switch/case for enum-to-string | JsonStringEnumConverter | Automatic, bidirectional, handles all enum values |
| Registry access | P/Invoke for RegOpenKey/RegSetValue | Microsoft.Win32.Registry | Managed wrapper handles key lifetime, value types, permissions |
| High-DPI form scaling | Manual pixel calculations | AutoScaleMode.Dpi | Framework handles DPI-aware control scaling |

**Key insight:** Every component in this phase uses inbox .NET APIs that are battle-tested. The risk is in the wiring (connecting settings changes to live detection), not in the individual pieces.

## Common Pitfalls

### Pitfall 1: Assembly.Location in Single-File Publish
**What goes wrong:** `Assembly.GetExecutingAssembly().Location` returns empty string for single-file published apps.
**Why it happens:** Single-file bundles extract to a temp directory; the assembly's notion of "location" is meaningless.
**How to avoid:** Use `Environment.ProcessPath` for the full exe path, or `AppContext.BaseDirectory` for the directory.
**Warning signs:** StartupManager registry value is empty or points to a temp extraction folder.

### Pitfall 2: Corrupt or Missing JSON on Load
**What goes wrong:** App crashes on startup if settings.json is hand-edited badly or contains unexpected JSON.
**Why it happens:** JsonSerializer.Deserialize throws on malformed JSON.
**How to avoid:** Wrap Load() in try/catch, fall back to `new AppSettings()` with defaults on any exception. The next Save() will overwrite the corrupt file.
**Warning signs:** Users report app won't start after editing settings.json.

### Pitfall 3: Registry Path Staleness After Move
**What goes wrong:** User moves the exe to a different folder; the HKCU Run value still points to the old path. App fails to start on next login.
**Why it happens:** The registry value is written once and never updated.
**How to avoid:** On app startup, if StartWithWindows is enabled in settings, verify the registry value matches the current `Environment.ProcessPath` and update it if not. This is a one-liner in ConfigManager.Load() or app init.
**Warning signs:** App was set to start with Windows but doesn't after being moved.

### Pitfall 4: Modal Dialog Blocking Hook Messages
**What goes wrong:** Concern that ShowDialog() blocks the message loop, preventing mouse hook callbacks.
**Why it's actually fine:** ShowDialog() runs its own nested message loop that dispatches all Windows messages, including WH_MOUSE_LL callbacks. The hook continues to work during a modal dialog. This is verified Windows behavior.
**Warning signs:** None -- this is a non-issue, but commonly worried about.

### Pitfall 5: Blurry Settings Dialog on High-DPI
**What goes wrong:** Controls in the settings form appear fuzzy/blurry on 125%+ display scaling.
**Why it happens:** Form does not set AutoScaleMode or uses the wrong mode.
**How to avoid:** Set `AutoScaleMode = AutoScaleMode.Dpi` on the SettingsForm. The app already sets `HighDpiMode.PerMonitorV2` in Program.cs.
**Warning signs:** Text looks slightly blurry, controls are misaligned on high-DPI screens.

### Pitfall 6: NumericUpDown Value Clamping
**What goes wrong:** User enters a value outside the min/max range; NumericUpDown silently clamps to the boundary without visual feedback.
**Why it happens:** Default WinForms behavior -- not a bug, but can confuse users.
**How to avoid:** Set reasonable Minimum and Maximum values: ZoneSize (1-50), DwellDelay (50-2000). These ranges cover all practical use cases.
**Warning signs:** User enters 0 for zone size; app gets 1 (minimum) -- working correctly but unexpectedly.

## Code Examples

### JSON Output Format
```json
{
  "Corner": "TopLeft",
  "ZoneSize": 10,
  "DwellDelayMs": 300,
  "StartWithWindows": false
}
```

### HotCorner Enum with ComboBox Binding
```csharp
// Populate ComboBox with enum values
_cornerCombo.DataSource = Enum.GetValues<HotCorner>();
_cornerCombo.SelectedItem = settings.Corner;

// Read back
var selected = (HotCorner)_cornerCombo.SelectedItem!;
```

### NumericUpDown Configuration
```csharp
// Zone size: 1-50 pixels
_zoneSizeInput = new NumericUpDown
{
    Minimum = 1,
    Maximum = 50,
    Value = settings.ZoneSize,
    Increment = 1
};

// Dwell delay: 50-2000 milliseconds
_dwellDelayInput = new NumericUpDown
{
    Minimum = 50,
    Maximum = 2000,
    Value = settings.DwellDelayMs,
    Increment = 50
};
```

### Form Layout with Labels
```csharp
// Manual layout approach (no designer)
var cornerLabel = new Label { Text = "Active Corner:", AutoSize = true, Location = new Point(12, 15) };
_cornerCombo = new ComboBox { Location = new Point(140, 12), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };

var zoneLabel = new Label { Text = "Zone Size (px):", AutoSize = true, Location = new Point(12, 48) };
_zoneSizeInput = new NumericUpDown { Location = new Point(140, 45), Width = 80 };

var delayLabel = new Label { Text = "Dwell Delay (ms):", AutoSize = true, Location = new Point(12, 81) };
_dwellDelayInput = new NumericUpDown { Location = new Point(140, 78), Width = 80 };

_startupCheckBox = new CheckBox { Text = "Start with Windows", AutoSize = true, Location = new Point(12, 114), Checked = StartupManager.IsEnabled };

var saveButton = new Button { Text = "Save", DialogResult = DialogResult.OK, Location = new Point(110, 150) };
var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Location = new Point(195, 150) };

AcceptButton = saveButton;
CancelButton = cancelButton;
ClientSize = new Size(310, 190);
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `Assembly.Location` | `Environment.ProcessPath` | .NET 6 (2021) | Required for single-file publish compatibility |
| `Newtonsoft.Json` | `System.Text.Json` | .NET Core 3.0 (2019) | Inbox, no NuGet dependency |
| `keybd_event` | `SendInput` | Win32 API (legacy vs recommended) | SendInput is atomic; keybd_event is deprecated |
| `JsonNamingPolicy` manual enum | `JsonStringEnumConverter` | .NET Core 3.0 | Automatic enum-to-string serialization |

**Deprecated/outdated:**
- `Assembly.Location`: Returns empty string in single-file publish. Use `Environment.ProcessPath`.
- Manual JSON string building: Use `JsonSerializer.Serialize()`.

## Open Questions

1. **SettingsForm: Designer vs. Manual Layout**
   - What we know: Both approaches work. Designer generates .Designer.cs files that are hard to review in diffs. Manual layout is more explicit.
   - What's unclear: Whether the planner prefers designer or code-only layout.
   - Recommendation: Use manual layout (no .Designer.cs). The form is simple (5 controls + 2 buttons) and code-only is easier to review and maintain.

2. **Registry path refresh on startup**
   - What we know: If the exe moves, the HKCU Run value becomes stale.
   - What's unclear: Whether this is a Phase 2 or Phase 3 concern.
   - Recommendation: Handle in Phase 2 as part of ConfigManager.Load() -- check and update registry path if StartWithWindows is true. One-liner, prevents a confusing user experience.

3. **HotCorner enum location after refactor**
   - What we know: HotCorner enum is currently in Core/CornerDetector.cs. Phase 2 needs it in Config/AppSettings.cs for serialization.
   - What's unclear: Whether to move the enum or have it shared.
   - Recommendation: Move HotCorner enum to Config/AppSettings.cs (or a shared namespace file). CornerDetector already uses it via `using` statement.

## Sources

### Primary (HIGH confidence)
- Phase 1 codebase (read directly) -- CornerDetector.cs, HotSpotApplicationContext.cs, Program.cs, NativeMethods.cs
- .planning/research/ARCHITECTURE.md -- Pattern 5 (JSON settings), Pattern 6 (StartupManager), project structure
- .planning/research/SUMMARY.md -- Phase 2 scope, stack decisions, pitfall catalog
- .planning/REQUIREMENTS.md -- CONF-01 through CONF-05, SETT-01 through SETT-05

### Secondary (MEDIUM confidence)
- System.Text.Json documentation (training knowledge, verified against inbox availability in .NET 10 csproj)
- Microsoft.Win32.Registry documentation (training knowledge, standard API unchanged since .NET Framework)
- WinForms AutoScaleMode.Dpi (training knowledge, consistent with Phase 1 DPI-awareness setup)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - All inbox .NET 10 APIs, verified against Phase 1 csproj. Zero NuGet dependencies.
- Architecture: HIGH - Patterns directly follow from Phase 1 code structure and architecture research. Settings wiring is straightforward single-thread update.
- Pitfalls: HIGH - Assembly.Location pitfall is well-documented. Corrupt JSON handling is standard practice. Registry staleness is a known pattern.

**Research date:** 2026-03-11
**Valid until:** 2026-04-11 (stable domain, no fast-moving dependencies)
