# Stack Research: WindowsHotSpot

**Confidence:** HIGH

## Recommended Stack

### Runtime & Framework

| Component | Recommendation | Rationale |
|-----------|---------------|-----------|
| .NET version | .NET 10.0 LTS | Current LTS (Nov 2028 support), SDK 10.0.103 available locally. Not .NET 8. |
| UI framework | WinForms | NotifyIcon + ContextMenuStrip + Form for settings. WPF adds XAML overhead for zero benefit in a tray app. |
| Project type | `net10.0-windows` WinForms | Gives access to System.Windows.Forms, System.Text.Json, Microsoft.Win32.Registry — all inbox, zero NuGet deps needed. |

### NuGet Dependencies

**None required.** All needed APIs are inbox:
- `System.Text.Json` — settings serialization
- `Microsoft.Win32.Registry` — "Start with Windows" run key
- `System.Windows.Forms` — tray icon, NotifyIcon, ContextMenuStrip, Form
- P/Invoke via `DllImport` — SetWindowsHookEx, SendInput (~10 lines of hand-written signatures)

### Key Win32 P/Invoke APIs

```csharp
// Global mouse hook
[DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
[DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hhk);
[DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
[DllImport("kernel32.dll")] static extern IntPtr GetModuleHandle(string lpModuleName);

// Trigger Win+Tab
[DllImport("user32.dll")] static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);
```

### Installer

| Option | Recommendation |
|--------|---------------|
| Tool | **Inno Setup 6.x** |
| Rationale | Simpler scripting than WiX, produces single setup.exe, supports `PrivilegesRequired=lowest` (no admin required for install). NSIS is older and more complex; WiX requires XML and MSBuild integration. |
| Output | Single `WindowsHotSpotSetup.exe` |

### Publishing

```xml
<!-- Self-contained single-file publish (no .NET runtime required on target machine) -->
<PublishSingleFile>true</PublishSingleFile>
<SelfContained>true</SelfContained>
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
```

**Important:** Use `AppContext.BaseDirectory` (not `Assembly.Location`) for config file path in single-file mode — `Assembly.Location` returns empty string when published as single file.

## What NOT to Use

| Technology | Reason |
|-----------|--------|
| WPF | XAML overhead, no benefit over WinForms for tray + single settings dialog |
| .NET Framework 4.x | Legacy, worse tooling, harder self-contained publish |
| WiX Toolset | XML-heavy, MSBuild integration complexity overkill for this app |
| NuGet packages for hooks | Hand-written P/Invoke is 10 lines; a package adds unnecessary dependency |
| Worker Service | No UI; need WinForms message loop to keep hook alive |

## Build Commands

```bash
# Build
dotnet build -c Release

# Publish self-contained single exe
dotnet publish -c Release -r win-x64 --self-contained true

# The installer script then wraps the published exe
```

---
*Research completed: 2026-03-11*
