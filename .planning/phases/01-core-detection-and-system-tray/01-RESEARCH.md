# Phase 1: Core Detection and System Tray - Research

**Researched:** 2026-03-11
**Domain:** Windows low-level mouse hook, system tray app, P/Invoke (C# / .NET 10 / WinForms)
**Confidence:** HIGH

## Summary

Phase 1 delivers the core value proposition: a system tray app that detects mouse dwell in a screen corner and triggers Task View via Win+Tab. This is the highest-risk phase because it involves P/Invoke for a global low-level mouse hook with strict timing constraints imposed by Windows -- if the hook callback exceeds ~300ms, Windows silently removes it with no notification. Everything else in the project depends on this phase working correctly.

The architecture is a single-STA-thread WinForms app using `ApplicationContext` (no main window). The mouse hook callback fires on the UI thread via the message loop, feeds coordinates to a corner detector with a dwell timer state machine, and fires `SendInput` for Win+Tab when the dwell completes. Phase 1 uses hard-coded settings (top-left corner, 300ms dwell, 10px zone) -- configuration is Phase 2. The system tray provides Quit, Settings (placeholder -- shows MessageBox in Phase 1), and About.

**Primary recommendation:** Build the hook, detection, and trigger first with hard-coded values. Get it working on a single monitor, then validate multi-monitor with Per-Monitor V2 DPI manifest. Add tray icon and menu last. The P/Invoke and hook timeout constraint are the only real risks.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| CORE-01 | Mouse dwell in corner zone triggers Task View | HookManager + CornerDetector + ActionTrigger pattern; dwell timer state machine; SendInput for Win+Tab |
| CORE-02 | Corner detection works on all monitors including different DPI | Per-Monitor V2 DPI manifest; Screen.AllScreens with unvirtualized coordinates; tested pattern documented |
| CORE-03 | Dwell timer configurable (default 300ms) | System.Windows.Forms.Timer; hard-coded 300ms in Phase 1, extracted to config in Phase 2 |
| CORE-04 | No trigger while mouse button held (drag suppression) | Check WM_LBUTTONDOWN/WM_RBUTTONDOWN in hook callback; suppress dwell while button state is down |
| CORE-05 | Re-arms only after cursor leaves zone (no double-trigger) | State machine: Idle -> Dwelling -> Triggered -> requires leave-zone to return to Idle |
| CORE-06 | Hook properly unregistered on exit including crash | IDisposable on HookManager; cleanup in ApplicationExit, try/finally, UnhandledException handler |
| TRAY-01 | No taskbar button | ApplicationContext pattern (no main form) -- nothing appears in taskbar |
| TRAY-02 | System tray icon present | NotifyIcon with embedded .ico resource |
| TRAY-03 | Tray menu: Settings, About, Quit | ContextMenuStrip with three ToolStripMenuItems |
| TRAY-04 | Settings opens settings dialog | Phase 1: show placeholder MessageBox; Phase 2: full SettingsForm |
| TRAY-05 | About shows app info | MessageBox.Show with app name, version, description |
| TRAY-06 | Quit cleanly exits and removes tray icon | ApplicationContext.ExitThread() in Quit handler; Dispose sets tray Visible=false |
</phase_requirements>

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| .NET 10.0 LTS | 10.0 | Runtime and SDK | Current LTS, all needed APIs inbox |
| WinForms | (inbox) | UI framework for tray icon, context menu, timer | Lighter than WPF for a tray-only app; no XAML overhead |
| System.Text.Json | (inbox) | JSON serialization (Phase 2, but define AppSettings now) | Inbox, no NuGet needed |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| user32.dll (P/Invoke) | N/A | SetWindowsHookEx, UnhookWindowsHookEx, CallNextHookEx, SendInput | Always -- no managed alternative for global mouse hook |
| kernel32.dll (P/Invoke) | N/A | GetModuleHandle | Hook installation requires module handle |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| WinForms | WPF | WPF is heavier, adds XAML complexity for zero visual benefit in a tray app |
| Raw P/Invoke | CsWin32 source generator | Adds build dependency; only ~10 P/Invoke signatures needed -- not worth it |
| System.Windows.Forms.Timer | System.Timers.Timer | System.Timers fires on threadpool -- would require Invoke() and risk cross-thread bugs |

**Installation:**
```bash
dotnet new winforms -n WindowsHotSpot --framework net10.0
```

## Architecture Patterns

### Recommended Project Structure
```
WindowsHotSpot/
  WindowsHotSpot.csproj
  app.manifest                    # Per-Monitor V2 DPI awareness
  Program.cs                      # [STAThread] entry, Application.Run(context)
  HotSpotApplicationContext.cs    # Owns lifetime, tray icon, wires components
  Core/
    HookManager.cs                # WH_MOUSE_LL install/uninstall, MouseMoved event
    CornerDetector.cs             # Zone check, dwell timer state machine
    ActionTrigger.cs              # SendInput for Win+Tab
  Native/
    NativeMethods.cs              # All DllImport, structs, constants
  Resources/
    app.ico                       # System tray icon (16x16, 32x32, 48x48)
```

### Pattern 1: ApplicationContext (No Main Form)
**What:** Subclass `ApplicationContext`, pass to `Application.Run()`. No form ever created. Message loop stays alive for hook.
**When:** Always -- this is the standard tray app pattern.
**Example:**
```csharp
// Source: Microsoft Learn - ApplicationContext
[STAThread]
static void Main()
{
    Application.EnableVisualStyles();
    Application.SetCompatibleTextRenderingDefault(false);
    Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
    Application.Run(new HotSpotApplicationContext());
}
```

### Pattern 2: Low-Level Mouse Hook with Pinned Delegate
**What:** `SetWindowsHookEx(WH_MOUSE_LL, callback, moduleHandle, 0)` with the delegate stored in a class-level field.
**When:** Always -- the delegate MUST be pinned to prevent GC collection.
**Critical:** The callback must return in <300ms. Only read coordinates, do bounds check, fire event, call `CallNextHookEx`.
**Example:**
```csharp
// Source: Microsoft Learn - SetWindowsHookExW
internal class HookManager : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private readonly NativeMethods.LowLevelMouseProc _hookCallback; // prevents GC

    public event Action<Point>? MouseMoved;
    public event Action<bool>? MouseButtonChanged; // true=down, false=up (for drag suppression)

    public HookManager()
    {
        _hookCallback = HookCallback;
    }

    public void Install()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        using var module = process.MainModule!;
        _hookId = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL, _hookCallback,
            NativeMethods.GetModuleHandle(module.ModuleName), 0);
        if (_hookId == IntPtr.Zero)
            throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            if (msg == NativeMethods.WM_MOUSEMOVE)
            {
                var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                MouseMoved?.Invoke(hookStruct.pt);
            }
            else if (msg == NativeMethods.WM_LBUTTONDOWN || msg == NativeMethods.WM_RBUTTONDOWN)
            {
                MouseButtonChanged?.Invoke(true);
            }
            else if (msg == NativeMethods.WM_LBUTTONUP || msg == NativeMethods.WM_RBUTTONUP)
            {
                MouseButtonChanged?.Invoke(false);
            }
        }
        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }
}
```

### Pattern 3: Dwell Timer State Machine
**What:** Four states -- Idle, Dwelling, Triggered, Cooldown. Requires mouse to leave zone before re-arming.
**When:** Always -- prevents double-trigger (Pitfall 7).
**Key detail:** Use `System.Windows.Forms.Timer` (fires on UI thread). Never use `System.Timers.Timer` or `System.Threading.Timer`.
```
State transitions:
  Idle + mouse enters zone + no button down -> start timer -> Dwelling
  Dwelling + mouse leaves zone -> stop timer -> Idle
  Dwelling + button down -> stop timer -> Idle
  Dwelling + timer fires -> send Win+Tab -> Triggered
  Triggered + mouse leaves zone -> Idle
  Triggered + mouse stays in zone -> stay Triggered (no re-fire)
```

### Pattern 4: Multi-Monitor Corner Detection
**What:** Check if mouse point is within N pixels of the configured corner on ANY connected screen.
**When:** On every WM_MOUSEMOVE from the hook.
**Critical:** With Per-Monitor V2 DPI manifest, `Screen.AllScreens[i].Bounds` and `MSLLHOOKSTRUCT.pt` are both in unvirtualized physical pixels -- they agree. Without the manifest, they can disagree by hundreds of pixels on mixed-DPI setups.
```csharp
// Corner point calculation from screen bounds
private static Point GetCornerPoint(Rectangle bounds, HotCorner corner) => corner switch
{
    HotCorner.TopLeft     => new Point(bounds.Left, bounds.Top),
    HotCorner.TopRight    => new Point(bounds.Right - 1, bounds.Top),
    HotCorner.BottomLeft  => new Point(bounds.Left, bounds.Bottom - 1),
    HotCorner.BottomRight => new Point(bounds.Right - 1, bounds.Bottom - 1),
    _ => throw new ArgumentOutOfRangeException()
};

// Zone check: is point within zoneSize pixels of corner?
bool inZone = Math.Abs(pt.X - corner.X) <= zoneSize
           && Math.Abs(pt.Y - corner.Y) <= zoneSize;
```

### Pattern 5: SendInput for Win+Tab (Atomic)
**What:** Four INPUT structs sent atomically: Win down, Tab down, Tab up, Win up.
**When:** When dwell timer fires.
```csharp
// Source: Microsoft Learn - SendInput
// 4 inputs sent atomically to prevent interleaving
var inputs = new NativeMethods.INPUT[4];
inputs[0] = MakeKeyInput(NativeMethods.VK_LWIN, keyUp: false);
inputs[1] = MakeKeyInput(NativeMethods.VK_TAB,  keyUp: false);
inputs[2] = MakeKeyInput(NativeMethods.VK_TAB,  keyUp: true);
inputs[3] = MakeKeyInput(NativeMethods.VK_LWIN, keyUp: true);
uint sent = NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
```

### Pattern 6: App Manifest for Per-Monitor V2 DPI
**What:** Declare DPI awareness in app.manifest so coordinates are unvirtualized.
**When:** Always -- must be present from Phase 1. Retrofitting DPI awareness is a rewrite.
```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
    </windowsSettings>
  </application>
</assembly>
```

Also set in .csproj:
```xml
<ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>
```

### Anti-Patterns to Avoid
- **Doing ANY work in the hook callback beyond coordinate reads and flag-setting:** Windows silently kills the hook after ~300ms. No I/O, no allocation, no logging, no UI calls.
- **Using a background thread for the hook:** WH_MOUSE_LL requires a message loop on the installing thread. No message loop = no callbacks.
- **Storing the hook delegate in a local variable or lambda:** GC collects it, causing crash or silent hook death.
- **Using System.Timers.Timer or Threading.Timer for dwell:** They fire on threadpool threads, causing cross-thread issues with SendInput and WinForms.
- **Using a hidden Form instead of ApplicationContext:** Unnecessary complexity, shows in Alt+Tab, handles WM_CLOSE unexpectedly.
- **Checking exact corner pixel instead of a zone:** Off-by-one and cursor clamping make single-pixel detection unreliable.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Global mouse monitoring | Custom polling loop | WH_MOUSE_LL via SetWindowsHookEx | Only supported way; polling would miss events and burn CPU |
| Input simulation | keybd_event calls | SendInput (4 INPUT structs) | SendInput is atomic (no interleaving), Microsoft-recommended replacement for keybd_event |
| System tray icon | Custom window + shell_notifyicon | WinForms NotifyIcon | Handles icon lifecycle, context menu, balloon tips automatically |
| Timer for dwell | Manual tick counting in hook callback | System.Windows.Forms.Timer | Fires on UI thread, integrates with message loop, no cross-thread issues |
| DPI awareness | Manual GetDpiForMonitor calls | App manifest + ApplicationHighDpiMode | Manifest declaration is the correct way; manual API calls are fragile and incomplete |

## Common Pitfalls

### Pitfall 1: Hook Timeout -- Silent Removal (CRITICAL)
**What goes wrong:** Hook callback takes >300ms, Windows silently removes hook. App stays in tray but stops detecting mouse.
**Why it happens:** Any work beyond trivial coordinate reads -- file I/O, logging, GC pause, modal dialog blocking message pump.
**How to avoid:** Callback does ONLY: read MSLLHOOKSTRUCT, bounds-check, set flag/fire event, call CallNextHookEx. All real processing happens in event handlers after the callback returns.
**Warning signs:** Hot corner stops working but tray icon is still visible. No errors in any log.

### Pitfall 2: GC Collects Hook Delegate
**What goes wrong:** Delegate passed to SetWindowsHookEx is collected by GC, causing crash (ExecutionEngineException) or silent hook death.
**Why it happens:** Delegate stored only as local variable or lambda -- no managed reference keeps it alive.
**How to avoid:** Store delegate in a class-level field: `private readonly LowLevelMouseProc _hookCallback;`
**Warning signs:** Random crashes that don't reproduce reliably, correlate with memory pressure.

### Pitfall 3: DPI Coordinate Mismatch on Multi-Monitor
**What goes wrong:** `Screen.Bounds` returns virtualized coordinates that disagree with hook's physical coordinates. Corner detection fails on non-primary monitors.
**Why it happens:** App runs as System DPI aware (default). Secondary monitors at different scaling get virtualized coordinates.
**How to avoid:** Declare Per-Monitor V2 in app.manifest AND set ApplicationHighDpiMode in .csproj. Must be done from Phase 1.
**Warning signs:** Works on primary monitor, fails on secondary. Works on single-monitor, fails on multi-monitor.

### Pitfall 4: Double-Trigger (Win+Tab Toggle)
**What goes wrong:** Dwell fires, sends Win+Tab (opens Task View). Mouse still in zone. Dwell fires again, sends Win+Tab (closes Task View). User sees flicker or nothing.
**Why it happens:** No state machine. Timer re-arms immediately after firing.
**How to avoid:** State machine requires mouse to LEAVE the zone before re-arming. Triggered state persists until mouse exits zone.
**Warning signs:** Task View flashes briefly then closes, or never appears at all.

### Pitfall 5: Hook Cleanup on Crash
**What goes wrong:** Orphaned hooks cause system-wide mouse lag.
**Why it happens:** Process exits without calling UnhookWindowsHookEx.
**How to avoid:** Multiple cleanup paths -- IDisposable, ApplicationExit event, try/finally around Application.Run(), UnhandledException handler.
**Warning signs:** Mouse feels sluggish after force-killing the app.

### Pitfall 6: Message Pump Starvation
**What goes wrong:** Modal dialog or long operation blocks message loop, hook stops receiving events, Windows may remove hook.
**Why it happens:** In Phase 1 this is low risk (no modal dialogs). Becomes relevant in Phase 2 when SettingsForm is added.
**How to avoid:** In Phase 1, keep the architecture clean. In Phase 2, show SettingsForm as a non-modal dialog or accept brief hook interruption during settings (user is actively interacting with settings, not trying to trigger hot corner).
**Warning signs:** Hook dies after opening settings dialog.

### Pitfall 7: UIPI Blocks SendInput to Elevated Apps
**What goes wrong:** Win+Tab silently dropped when elevated app (Task Manager as admin, UAC prompt) is focused.
**Why it happens:** UIPI prevents medium-integrity process from sending input to high-integrity process.
**How to avoid:** Accept as known limitation for Phase 1. Win+Tab targets Explorer shell (medium integrity), so it MAY work even with elevated foreground -- needs empirical testing. If blocked, UIAccess manifest is a Phase 3 concern.
**Warning signs:** Hot corner works most of the time but fails when elevated apps are focused.

## Code Examples

### Complete NativeMethods.cs (All P/Invoke for Phase 1)
```csharp
// Source: Microsoft Learn - Win32 API documentation
using System.Runtime.InteropServices;

namespace WindowsHotSpot.Native;

internal static class NativeMethods
{
    // Hook constants
    public const int WH_MOUSE_LL = 14;

    // Mouse messages
    public const int WM_MOUSEMOVE    = 0x0200;
    public const int WM_LBUTTONDOWN  = 0x0201;
    public const int WM_LBUTTONUP    = 0x0202;
    public const int WM_RBUTTONDOWN  = 0x0204;
    public const int WM_RBUTTONUP    = 0x0205;

    // Input constants
    public const uint INPUT_KEYBOARD    = 1;
    public const uint KEYEVENTF_KEYUP   = 0x0002;
    public const ushort VK_LWIN         = 0x5B;
    public const ushort VK_TAB          = 0x09;

    // Delegate for low-level mouse hook
    public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    // Hook structs
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;

        public static implicit operator System.Drawing.Point(POINT p) => new(p.X, p.Y);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    // SendInput structs
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    // P/Invoke declarations
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook,
        LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk,
        int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs,
        [MarshalAs(UnmanagedType.LPArray)] INPUT[] pInputs, int cbSize);
}
```

### App Manifest (app.manifest)
```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="1.0.0.0" name="WindowsHotSpot"/>
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v2">
    <security>
      <requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3">
        <requestedExecutionLevel level="asInvoker" uiAccess="false" />
      </requestedPrivileges>
    </security>
  </trustInfo>
  <compatibility xmlns="urn:schemas-microsoft-com:compatibility.v1">
    <application>
      <supportedOS Id="{8e0f7a12-bfb3-4fe8-b9a5-48fd50a15a9a}" />
    </application>
  </compatibility>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
      <dpiAware xmlns="http://schemas.microsoft.com/SMI/2005/WindowsSettings">true/pm</dpiAware>
    </windowsSettings>
  </application>
</assembly>
```

### .csproj Key Settings
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWindowsForms>true</UseWindowsForms>
    <ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>
    <ApplicationIcon>Resources\app.ico</ApplicationIcon>
    <ApplicationManifest>app.manifest</ApplicationManifest>
  </PropertyGroup>
</Project>
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `keybd_event` for input simulation | `SendInput` with INPUT array | Windows XP+ | SendInput is atomic, prevents interleaving |
| System DPI aware (default) | Per-Monitor V2 DPI awareness | Windows 10 1703 | Required for correct coordinates on mixed-DPI multi-monitor |
| `Application.Run(hiddenForm)` | `Application.Run(new ApplicationContext())` | Always available, but hidden-form pattern persists in examples | Cleaner, no Alt+Tab ghost window |
| `Assembly.Location` for exe path | `AppContext.BaseDirectory` | .NET 5+ single-file publish | `Assembly.Location` returns empty string in single-file publish |

## Architecture Decision: Single UI Thread (Recommended for Phase 1)

The prior research surfaced a tension: Architecture research recommends a single STA thread (simpler), while Pitfalls research warns that modal dialogs can stall the message pump and kill the hook (Pitfall 6).

**Decision for Phase 1:** Use the single UI thread. Rationale:
1. Phase 1 has no modal dialogs (Settings placeholder is a MessageBox which pumps messages and returns quickly).
2. The hook callback is trivially fast (coordinate read + bounds check + event fire).
3. A dedicated hook thread adds complexity (cross-thread marshaling, two message loops) with no benefit until Phase 2 introduces the SettingsForm.
4. In Phase 2, the SettingsForm can be shown non-modally (`.Show()` not `.ShowDialog()`), or the brief stall during modal settings is acceptable since the user is actively interacting with settings and not trying to trigger the hot corner.

If Phase 2 testing reveals the modal dialog does kill the hook, the migration path is: move hook installation to a dedicated thread with its own `Application.Run()` message loop, and marshal events to the UI thread via `SynchronizationContext.Post`. This is a contained change to HookManager only.

## Drag Suppression Implementation

CORE-04 requires suppressing hot corner while a mouse button is held (drag). The hook already sees WM_LBUTTONDOWN/UP and WM_RBUTTONDOWN/UP messages. Implementation:

1. HookManager tracks button state via MouseButtonChanged event (or a simple boolean).
2. CornerDetector checks button state before starting dwell timer.
3. If button goes down during dwell, cancel the timer immediately.
4. This prevents accidental triggers when the user is dragging a window near a corner.

## Open Questions

1. **UIPI + Win+Tab empirical behavior**
   - What we know: SendInput is blocked by UIPI for elevated foreground apps (documented).
   - What's unclear: Win+Tab targets Explorer shell (medium integrity). It may bypass UIPI because the shell processes Win key combos at a lower level.
   - Recommendation: Test empirically in Phase 1. Run Task Manager as admin, focus it, trigger hot corner. If it works, document and move on. If it fails, accept as known limitation for v1.

2. **Icon resource for tray**
   - What we know: NotifyIcon requires an Icon object (.ico format).
   - What's unclear: Whether to embed a custom icon or use a system icon initially.
   - Recommendation: Create a simple placeholder .ico (can be generated from a 16x16 PNG). A proper icon is a Phase 3 polish item. For Phase 1, `SystemIcons.Application` works as a fallback.

3. **Hook health monitoring**
   - What we know: Hook removal is silent -- no API to query "is my hook still installed?"
   - What's unclear: How aggressive to be about re-installation.
   - Recommendation: For Phase 1, focus on correctness (trivial callback, proper cleanup). Health monitoring (periodic re-install timer) can be added in Phase 3 polish if silent failures are observed in testing.

## Sources

### Primary (HIGH confidence)
- [SetWindowsHookExW -- Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowshookexw) -- hook installation, timeout, delegate pinning
- [LowLevelMouseProc -- Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc) -- callback constraints, message loop requirement
- [SendInput -- Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput) -- input simulation, UIPI limitations
- [ApplicationContext -- Microsoft Learn](https://learn.microsoft.com/en-us/dotnet/api/system.windows.forms.applicationcontext) -- tray app pattern
- [High DPI Desktop Application Development -- Microsoft Learn](https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows) -- Per-Monitor V2, coordinate virtualization

### Secondary (MEDIUM confidence)
- Project research: .planning/research/ARCHITECTURE.md -- component design, code patterns
- Project research: .planning/research/PITFALLS.md -- 14 pitfalls catalogued with mitigations
- Competitor analysis: vhanla/winxcorners (907 stars), flexits/HotCornersWin (127 stars)

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH -- zero external dependencies, all inbox .NET 10 APIs, verified with Microsoft docs
- Architecture: HIGH -- single-thread ApplicationContext pattern is the documented standard for WinForms tray apps
- P/Invoke signatures: HIGH -- verified against Microsoft Win32 API docs; complete struct layouts included
- Pitfalls: HIGH -- all sourced from official Microsoft documentation with specific doc URLs
- Multi-monitor DPI: HIGH -- Per-Monitor V2 manifest approach verified against Microsoft high-DPI docs
- UIPI interaction: MEDIUM -- constraint is documented but Win+Tab shell bypass behavior needs empirical testing

**Research date:** 2026-03-11
**Valid until:** 2026-04-11 (stable domain, Win32 APIs rarely change)
