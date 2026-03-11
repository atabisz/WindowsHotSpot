# Architecture Patterns

**Domain:** Windows system tray application with global mouse hook
**Researched:** 2026-03-11
**Confidence:** HIGH (verified against official Microsoft documentation)

## Recommended Architecture

WindowsHotSpot follows a standard "invisible tray app with hook" pattern. There is no main window. The application runs as a custom `ApplicationContext` subclass passed to `Application.Run()`, which provides the message loop that the low-level mouse hook requires.

```
Program.Main()
  |
  +-- Application.Run(new HotSpotApplicationContext())
        |
        +-- ConfigManager (loads JSON settings)
        +-- NotifyIcon (system tray icon + context menu)
        +-- HookManager (installs WH_MOUSE_LL)
        |     |
        |     +-- LowLevelMouseProc callback (on UI thread via message pump)
        |           |
        |           +-- CornerDetector.OnMouseMove(point)
        |                 |
        |                 +-- Dwell timer check
        |                 +-- ActionTrigger.SendTaskView() (Win+Tab via SendInput)
        |
        +-- SettingsForm (shown on demand from tray menu)
```

### Component Boundaries

| Component | Responsibility | Communicates With |
|-----------|---------------|-------------------|
| `Program` | Entry point. Sets `[STAThread]`, calls `Application.Run(context)` | `HotSpotApplicationContext` |
| `HotSpotApplicationContext` | Owns app lifetime, creates/disposes all components, no main form | `ConfigManager`, `HookManager`, `NotifyIcon`, `SettingsForm` |
| `HookManager` | Installs/uninstalls WH_MOUSE_LL hook via P/Invoke. Exposes `MouseMoved` event | `CornerDetector` (via event) |
| `CornerDetector` | Determines if mouse is in hot corner zone, manages dwell timer | `HookManager` (receives events), `ActionTrigger` (fires trigger), `ConfigManager` (reads settings) |
| `ActionTrigger` | Sends Win+Tab keystroke via SendInput P/Invoke | Called by `CornerDetector` |
| `ConfigManager` | Loads/saves `AppSettings` from/to JSON file next to exe | `CornerDetector`, `SettingsForm`, `StartupManager` |
| `NotifyIcon` (system class) | System tray icon with context menu (Settings, About, Quit) | `HotSpotApplicationContext` (menu handlers) |
| `SettingsForm` | WinForms dialog for configuring corner, zone size, dwell delay, startup | `ConfigManager` |
| `StartupManager` | Reads/writes `HKCU\...\Run` registry key for "Start with Windows" | `SettingsForm`, `ConfigManager` |

### Data Flow

**Mouse event flow (critical path):**

```
1. User moves mouse anywhere on any screen
2. Windows sends message to the thread that installed WH_MOUSE_LL
3. WinForms message loop dispatches to LowLevelMouseProc callback
4. HookManager fires MouseMoved event with screen coordinates (POINT from MSLLHOOKSTRUCT)
5. CornerDetector checks: is point within zone pixels of configured corner on any screen?
6. If YES and not already dwelling: start dwell timer (System.Windows.Forms.Timer)
7. If mouse leaves zone: cancel dwell timer
8. When dwell timer fires: ActionTrigger.SendTaskView()
9. ActionTrigger sends Win+Tab via SendInput (4 INPUT structs: VK_LWIN down, VK_TAB down, VK_TAB up, VK_LWIN up)
10. Set cooldown flag to prevent re-triggering until mouse leaves zone
```

**Settings flow:**

```
1. App starts -> ConfigManager.Load() reads JSON from Path.Combine(AppContext.BaseDirectory, "settings.json")
2. User opens Settings dialog from tray menu
3. SettingsForm reads current values from ConfigManager
4. User changes settings, clicks Save
5. ConfigManager.Save() writes JSON, fires SettingsChanged event
6. CornerDetector picks up new values (corner, zone size, dwell delay)
```

## Patterns to Follow

### Pattern 1: ApplicationContext Without a Main Form
**What:** Subclass `ApplicationContext` and pass it to `Application.Run()` instead of a form. This keeps the message loop alive without showing any window.
**When:** Always -- this is the standard pattern for system tray apps.
**Why:** The WH_MOUSE_LL hook requires the installing thread to pump messages. `Application.Run(context)` provides exactly that.
**Example:**
```csharp
internal class HotSpotApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly HookManager _hookManager;
    private readonly CornerDetector _cornerDetector;
    private readonly ConfigManager _configManager;

    public HotSpotApplicationContext()
    {
        _configManager = new ConfigManager();
        _configManager.Load();

        _cornerDetector = new CornerDetector(_configManager.Settings);
        _cornerDetector.Triggered += OnHotCornerTriggered;

        _hookManager = new HookManager();
        _hookManager.MouseMoved += _cornerDetector.OnMouseMove;
        _hookManager.Install();

        _trayIcon = new NotifyIcon
        {
            Icon = Resources.AppIcon,
            Text = "WindowsHotSpot",
            Visible = true,
            ContextMenuStrip = BuildContextMenu()
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hookManager.Uninstall();
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
```

### Pattern 2: Low-Level Mouse Hook via P/Invoke
**What:** Use `SetWindowsHookEx(WH_MOUSE_LL, ...)` with a static callback delegate pinned in a field to prevent GC collection.
**When:** Always -- there is no managed alternative for global mouse monitoring.
**Critical detail:** The callback delegate MUST be stored in a class-level field (not a local variable) to prevent the garbage collector from collecting it, which would crash the application.
**Example:**
```csharp
internal class HookManager : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private IntPtr _hookId = IntPtr.Zero;
    private readonly LowLevelMouseProc _hookCallback; // prevents GC collection

    public event Action<Point>? MouseMoved;

    public HookManager()
    {
        _hookCallback = HookCallback; // store delegate reference
    }

    public void Install()
    {
        using var process = Process.GetCurrentProcess();
        using var module = process.MainModule!;
        _hookId = SetWindowsHookEx(WH_MOUSE_LL, _hookCallback,
            GetModuleHandle(module.ModuleName), 0);
    }

    public void Uninstall()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && wParam == (IntPtr)WM_MOUSEMOVE)
        {
            var hookStruct = Marshal.PtrToStructure<MSLLHOOKSTRUCT>(lParam);
            MouseMoved?.Invoke(hookStruct.pt);
        }
        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    // P/Invoke declarations...
    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")] private static extern IntPtr SetWindowsHookEx(int idHook,
        LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll")] private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll")] private static extern IntPtr CallNextHookEx(IntPtr hhk,
        int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string lpModuleName);
}
```

### Pattern 3: Corner Detection with Dwell Timer
**What:** Check if mouse position falls within a zone of N pixels from a screen corner, using `Screen.AllScreens` for multi-monitor awareness. Use a `System.Windows.Forms.Timer` for the dwell delay.
**When:** On every WM_MOUSEMOVE that arrives via the hook.
**Why System.Windows.Forms.Timer:** It fires on the UI thread (via the message loop), so there are no cross-thread issues when triggering SendInput. Do NOT use `System.Timers.Timer` or `System.Threading.Timer` which fire on thread pool threads.
**Example:**
```csharp
internal class CornerDetector
{
    private readonly System.Windows.Forms.Timer _dwellTimer;
    private bool _isInZone;
    private bool _hasTriggered;

    public event Action? Triggered;

    public CornerDetector(AppSettings settings)
    {
        _dwellTimer = new System.Windows.Forms.Timer { Interval = settings.DwellDelayMs };
        _dwellTimer.Tick += (_, _) =>
        {
            _dwellTimer.Stop();
            _hasTriggered = true;
            Triggered?.Invoke();
        };
    }

    public void OnMouseMove(Point screenPoint)
    {
        bool inZone = IsInHotCorner(screenPoint);

        if (inZone && !_isInZone && !_hasTriggered)
        {
            _isInZone = true;
            _dwellTimer.Start();
        }
        else if (!inZone)
        {
            _isInZone = false;
            _hasTriggered = false;
            _dwellTimer.Stop();
        }
    }

    private bool IsInHotCorner(Point pt)
    {
        foreach (var screen in Screen.AllScreens)
        {
            Point corner = GetCornerPoint(screen.Bounds, _settings.Corner);
            if (Math.Abs(pt.X - corner.X) <= _settings.ZoneSize &&
                Math.Abs(pt.Y - corner.Y) <= _settings.ZoneSize)
                return true;
        }
        return false;
    }
}
```

### Pattern 4: SendInput for Win+Tab
**What:** Use the Win32 `SendInput` API to simulate pressing and releasing Win+Tab.
**When:** When the dwell timer fires (hot corner triggered).
**Why SendInput over keybd_event:** Microsoft recommends SendInput; it inserts events atomically as a group, preventing other input from interleaving. `keybd_event` is legacy.
**Example:**
```csharp
internal static class ActionTrigger
{
    public static void SendTaskView()
    {
        var inputs = new INPUT[4];

        inputs[0] = MakeKeyInput(VK_LWIN, false);  // Win down
        inputs[1] = MakeKeyInput(VK_TAB, false);    // Tab down
        inputs[2] = MakeKeyInput(VK_TAB, true);     // Tab up
        inputs[3] = MakeKeyInput(VK_LWIN, true);    // Win up

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    private static INPUT MakeKeyInput(ushort vk, bool keyUp)
    {
        return new INPUT
        {
            type = INPUT_KEYBOARD,
            u = new InputUnion
            {
                ki = new KEYBDINPUT
                {
                    wVk = vk,
                    dwFlags = keyUp ? KEYEVENTF_KEYUP : 0u
                }
            }
        };
    }

    // P/Invoke: SendInput, INPUT, KEYBDINPUT structs, VK_LWIN = 0x5B, VK_TAB = 0x09
}
```

### Pattern 5: JSON Settings File Next to Exe
**What:** Store settings in a `settings.json` file in the same directory as the executable. Use `System.Text.Json` (built into .NET 8).
**When:** Load on startup, save when user clicks Save in the settings dialog.
**Why next to exe:** Simple, portable, user can inspect/copy the file. No AppData folder needed for a single-file utility.
**Example:**
```csharp
internal class ConfigManager
{
    private static readonly string SettingsPath =
        Path.Combine(AppContext.BaseDirectory, "settings.json");

    public AppSettings Settings { get; private set; } = new();

    public void Load()
    {
        if (File.Exists(SettingsPath))
        {
            var json = File.ReadAllText(SettingsPath);
            Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
        }
    }

    public void Save()
    {
        var json = JsonSerializer.Serialize(Settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(SettingsPath, json);
        SettingsChanged?.Invoke();
    }

    public event Action? SettingsChanged;
}

internal class AppSettings
{
    public HotCorner Corner { get; set; } = HotCorner.TopLeft;
    public int ZoneSize { get; set; } = 5;
    public int DwellDelayMs { get; set; } = 300;
    public bool StartWithWindows { get; set; } = false;
}

internal enum HotCorner { TopLeft, TopRight, BottomLeft, BottomRight }
```

### Pattern 6: Start with Windows via Registry
**What:** Add/remove a value under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` containing the path to the exe.
**When:** When the user toggles the "Start with Windows" checkbox.
**Why HKCU not HKLM:** HKCU does not require admin elevation. HKLM requires running as administrator.
**Example:**
```csharp
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
            key.SetValue(AppName, $"\"{Environment.ProcessPath}\"");
        else
            key.DeleteValue(AppName, false);
    }
}
```

## Anti-Patterns to Avoid

### Anti-Pattern 1: Doing Work in the Hook Callback
**What:** Performing anything slow (file I/O, network, UI updates, logging) inside the `LowLevelMouseProc` callback.
**Why bad:** Windows enforces a timeout on low-level hook callbacks (default ~300ms, max 1000ms on Windows 10 1709+). If the callback exceeds this, **Windows silently removes the hook on Windows 7+** with no notification. The app stops detecting mouse moves and the user has no indication.
**Instead:** The callback should only: (1) read the MSLLHOOKSTRUCT, (2) fire an event or set a flag, (3) call `CallNextHookEx`, and return. All processing happens in event handlers on the same thread's message loop, which runs after the hook returns.

### Anti-Pattern 2: Using a Background Thread for the Hook
**What:** Installing `SetWindowsHookEx(WH_MOUSE_LL, ...)` on a thread that does not pump messages.
**Why bad:** The low-level hook callback is delivered via the message loop of the installing thread. No message loop = no callbacks = hook is useless. On Windows 7+ the hook will be silently removed.
**Instead:** Install the hook on the main UI thread (the one running `Application.Run()`). The WinForms message loop is the hook's message pump.

### Anti-Pattern 3: Using System.Timers.Timer or Threading.Timer for Dwell
**What:** Using a non-UI timer to implement dwell delay.
**Why bad:** These timers fire on thread pool threads. Calling `SendInput` from a non-UI thread may fail or behave unpredictably. Updating UI state from a thread pool thread requires `Invoke()` and introduces race conditions.
**Instead:** Use `System.Windows.Forms.Timer` which fires its `Tick` event on the UI thread via the message loop.

### Anti-Pattern 4: Using Application.Run() with a Hidden Form
**What:** Creating a `Form` with `ShowInTaskbar = false` and `WindowState = Minimized` as the main form.
**Why bad:** The form still exists, still processes `WM_CLOSE` (user could accidentally close it), adds unnecessary complexity, and appears in Alt+Tab.
**Instead:** Use `ApplicationContext` with no main form. The `NotifyIcon` is the only visible UI element. Call `ExitThread()` on the context to terminate the app.

### Anti-Pattern 5: Forgetting to Pin the Delegate
**What:** Creating the `LowLevelMouseProc` delegate as a lambda or local variable without storing it in a field.
**Why bad:** The garbage collector can collect the delegate since nothing references it from managed code. The native hook still holds the function pointer, causing an `ExecutionEngineException` crash. This is explicitly called out in the [Microsoft documentation for SetWindowsHookEx](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowshookexw).
**Instead:** Store the delegate in a class-level field (see HookManager pattern above).

## Threading Model

This application is single-threaded from an architectural perspective:

```
[STAThread] Main Thread (UI Thread)
  |
  +-- Application.Run() pumps messages
  |     |
  |     +-- WH_MOUSE_LL callback invoked via message dispatch
  |     +-- System.Windows.Forms.Timer.Tick fires via message dispatch
  |     +-- NotifyIcon context menu handlers fire via message dispatch
  |     +-- SettingsForm shown modally on this thread
  |
  +-- All P/Invoke calls (SetWindowsHookEx, SendInput, Registry) happen here
```

**There are no background threads.** The hook callback, timers, SendInput, settings I/O, and UI all run on the single STA thread. This is by design:

1. WH_MOUSE_LL requires a message loop on the installing thread
2. SendInput should be called from the thread that installed the hook (same desktop context)
3. WinForms controls are not thread-safe
4. The app does so little work that a single thread is more than sufficient

## Multi-Monitor Corner Detection

The `MSLLHOOKSTRUCT.pt` field provides coordinates in per-monitor-aware screen space. For multi-monitor setups:

- `Screen.AllScreens` returns all connected monitors with their `Bounds` (position and size in virtual screen coordinates)
- The virtual screen can have negative coordinates (monitors to the left of or above the primary monitor)
- Corner points are calculated from each screen's `Bounds`:
  - TopLeft: `(Bounds.Left, Bounds.Top)`
  - TopRight: `(Bounds.Right - 1, Bounds.Top)`
  - BottomLeft: `(Bounds.Left, Bounds.Bottom - 1)`
  - BottomRight: `(Bounds.Right - 1, Bounds.Bottom - 1)`
- The zone check uses `Math.Abs(pt.X - corner.X) <= zoneSize` (not a simple rectangle), treating the corner as a square zone

## Build Order Implications

Components should be built in this order based on dependencies:

| Phase | Components | Rationale |
|-------|-----------|-----------|
| 1 | `Program`, `HotSpotApplicationContext`, `HookManager`, `CornerDetector`, `ActionTrigger` | Core loop: hook -> detect -> trigger. This is the MVP. Can hard-code settings. |
| 2 | `ConfigManager`, `AppSettings` | Extract hard-coded values into JSON persistence. |
| 3 | `NotifyIcon` + context menu, `SettingsForm` | User-facing controls. Requires ConfigManager to be useful. |
| 4 | `StartupManager` | Registry integration. Independent but logically follows settings UI. |
| 5 | Installer (NSIS/WiX) | Packaging. Requires all other components to be complete. |

**Phase 1 is the riskiest** because it involves P/Invoke and the hook timeout constraint. Get this working first to validate the core mechanism before building settings and UI.

## Project Structure

```
WindowsHotSpot/
  WindowsHotSpot.csproj
  Program.cs
  HotSpotApplicationContext.cs
  Core/
    HookManager.cs           -- SetWindowsHookEx, LowLevelMouseProc
    CornerDetector.cs         -- Zone detection, dwell timer
    ActionTrigger.cs          -- SendInput for Win+Tab
  Config/
    ConfigManager.cs          -- JSON load/save
    AppSettings.cs            -- Settings POCO + enum
    StartupManager.cs         -- Registry run key
  UI/
    SettingsForm.cs           -- WinForms dialog (designer)
    SettingsForm.Designer.cs
  Native/
    NativeMethods.cs          -- All P/Invoke declarations in one place
  Resources/
    app.ico                   -- Tray icon
  Properties/
    launchSettings.json
```

Keeping all P/Invoke declarations in a single `NativeMethods.cs` file is the standard .NET convention. It centralizes the `DllImport` declarations, struct definitions (`INPUT`, `KEYBDINPUT`, `MSLLHOOKSTRUCT`), and constants (`WH_MOUSE_LL`, `WM_MOUSEMOVE`, `VK_LWIN`, `VK_TAB`, etc.).

## Sources

- [SetWindowsHookExW -- Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowshookexw) (HIGH confidence, official Win32 API docs)
- [LowLevelMouseProc callback -- Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc) (HIGH confidence, documents timeout behavior and message loop requirement)
- [MSLLHOOKSTRUCT -- Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-msllhookstruct) (HIGH confidence, documents per-monitor-aware coordinates)
- [SendInput -- Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput) (HIGH confidence, official API docs with Win+D example adapted for Win+Tab)
- [ApplicationContext -- Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.applicationcontext) (HIGH confidence, official .NET API docs with system tray pattern example)
