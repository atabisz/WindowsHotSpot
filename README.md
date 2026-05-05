# WindowsHotSpot

Hot corners and window drag for Windows. Move your mouse to a configured screen corner to trigger an action — or hold Ctrl+Alt and drag any window to move it without grabbing the title bar.

## Features

- **Hot corners** — configurable action per corner per monitor: Win+Tab (Task View), Show Desktop, Action Center, custom keystroke, or disabled
- **Window drag anywhere** — hold Ctrl+Alt and left-click-drag anywhere on a window to move it (no title bar required)
- Configurable zone size and dwell delay
- Drag suppression — dragging a window into the corner does not trigger the corner action
- Multi-monitor aware — independent corner config per monitor; works across monitors at different DPI scaling
- System tray icon — no taskbar button
- Settings dialog with immediate effect (no restart required)
- Start with Windows option
- Self-contained executable — no .NET runtime required on target machine
- No-admin installer

## Installation

Download `WindowsHotSpot-Setup.exe` from the [releases page](../../releases) and run it.

The installer does not require admin elevation. The app installs to `%LocalAppData%\WindowsHotSpot\`.

### Windows security warnings

Windows and browsers will likely warn you when downloading or running the installer — SmartScreen may say "Windows protected your PC" and browsers may flag it as untrusted. This is expected: the executable is not code-signed (certificates cost money and this is a free project).

If you're not comfortable dismissing those warnings, download the source and build it yourself — instructions are in the [Building from source](#building-from-source) section below.

## Usage

The app starts in the system tray. Right-click the tray icon to access:

- **Settings** — configure corners, zone size, dwell delay, startup behavior, and window drag options
- **About** — version info
- **Quit** — exit the application

### Hot corners

Move the cursor to a configured corner and hold it there for the dwell delay. The configured action fires.

### Window drag anywhere

Hold **Left Ctrl + Left Alt**, then left-click and drag anywhere on any restored (non-maximized) window to move it. The initiating click is not forwarded to the application — the window just moves.

- Maximized windows are skipped — the click passes through normally
- Elevated (admin) windows are skipped — UIPI prevents cross-privilege window moves
- AltGr (Right Ctrl + Left Alt) does not trigger drag
- Releasing Ctrl or Alt mid-drag cancels the drag and leaves the window at its current position

### Settings

| Setting | Default | Description |
|---------|---------|-------------|
| Zone size | 10 px | Size of the corner detection area |
| Dwell delay | 300 ms | How long the cursor must stay in the corner before triggering |
| Start with Windows | Off | Launch automatically at login |
| Pass through clicks when no window is draggable | Off | When Ctrl+Alt+click lands on a non-draggable surface, pass the click through instead of swallowing it |

Per-monitor corner configuration is available in Settings — select a monitor, configure its four corners independently, or use "Same on all monitors".

Settings are saved to `settings.json` next to the executable and take effect immediately on Save.

## Building from source

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Inno Setup 6](https://jrsoftware.org/isinfo.php) (for building the installer)

### Run from source

```powershell
cd WindowsHotSpot
dotnet run
```

### Build release installer

```powershell
.\build.ps1
```

This will:
1. Read the version from `WindowsHotSpot/WindowsHotSpot.csproj`
2. Patch `installer/WindowsHotSpot.iss` with the current version
3. Publish a self-contained single-file executable
4. Compile the Inno Setup installer to `dist/WindowsHotSpot-Setup.exe`

### Releasing a new version

1. Update `<Version>` in [WindowsHotSpot/WindowsHotSpot.csproj](WindowsHotSpot/WindowsHotSpot.csproj)
2. Run `.\build.ps1`
3. Distribute `dist/WindowsHotSpot-Setup.exe`

The installer `AppVersion` is patched automatically from the csproj — no manual sync needed.

## Project structure

```
WindowsHotSpot/
├── Native/
│   └── NativeMethods.cs       # P/Invoke: WH_MOUSE_LL, WH_KEYBOARD_LL, SetWindowPos, etc.
├── Core/
│   ├── HookManager.cs         # Global mouse hook (WH_MOUSE_LL) with suppression predicate
│   ├── CornerDetector.cs      # Dwell state machine, drag suppression, multi-monitor
│   ├── CornerRouter.cs        # Per-(monitor, corner) detector pool; rebuilt on settings change
│   ├── WindowDragHandler.cs   # Ctrl+Alt drag: WH_KEYBOARD_LL + SetWindowPos
│   └── ActionDispatcher.cs    # Dispatches CornerAction to SendInput
├── Config/
│   ├── AppSettings.cs         # Settings model
│   ├── ConfigManager.cs       # JSON persistence, SettingsChanged event
│   └── StartupManager.cs      # HKCU Run registry key
├── UI/
│   ├── SettingsForm.cs        # Settings dialog
│   └── KeyRecorderPanel.cs    # Hotkey recorder
├── HotSpotApplicationContext.cs  # App entry point (tray icon, no taskbar button)
├── Program.cs
├── app.manifest               # Per-Monitor V2 DPI awareness declaration
└── WindowsHotSpot.csproj
installer/
└── WindowsHotSpot.iss         # Inno Setup script (AppVersion patched by build.ps1)
dist/
└── WindowsHotSpot-Setup.exe   # Built installer (not committed)
build.ps1                      # Build script: publish + installer
```

## Technical notes

- Uses `WH_MOUSE_LL` (low-level global mouse hook) for corner detection and `WH_KEYBOARD_LL` for Ctrl+Alt modifier tracking — both run on the UI thread via the Windows message loop
- Hook callbacks are kept minimal to avoid Windows silently removing them due to timeout (< 300 ms rule)
- Hook delegates are pinned in class fields to prevent garbage collection
- Coordinates from `WH_MOUSE_LL` (`MSLLHOOKSTRUCT.pt`) are in physical pixels; `Screen.AllScreens` bounds match when Per-Monitor V2 DPI awareness is declared in the manifest
- `AppContext.BaseDirectory` is used for the settings file path (`Assembly.Location` returns empty for single-file published executables)
- Window drag uses `WindowFromPoint` → `GetAncestor(GA_ROOT)` → elevation check → `SetWindowPos(SWP_ASYNCWINDOWPOS)` to move windows without blocking the hook callback
- AltGr protection: AltGr synthesises a fake `VK_LCONTROL` event with `LLKHF_INJECTED` set — the keyboard hook skips injected LCtrl events

## Known limitations

- Elevated (admin) windows cannot be dragged — UIPI prevents `SetWindowPos` from a non-elevated process. The click passes through normally.
- When an elevated application is in the foreground, `SendInput` for corner actions may be blocked by UIPI. Task View is processed by Explorer at medium integrity, so in practice corner actions rarely hit this.
- Multiple simultaneous hot corners are not supported.
