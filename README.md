# WindowsHotSpot

Hot corner trigger for Windows Task View. Move your mouse to a configured screen corner, hold it briefly, and Task View opens — on any connected monitor.

## Features

- Configurable corner (Top-Left, Top-Right, Bottom-Left, Bottom-Right)
- Configurable zone size and dwell delay
- Drag suppression — dragging a window into the corner does not trigger
- Multi-monitor aware — works correctly across monitors at different DPI scaling
- System tray icon — no taskbar button
- Settings dialog with immediate effect (no restart required)
- Start with Windows option
- Self-contained executable — no .NET runtime required on target machine
- No-admin installer

## Installation

Download `WindowsHotSpot-Setup.exe` from the [releases page](../../releases) and run it.

The installer does not require admin elevation. The app installs to `%LocalAppData%\WindowsHotSpot\`.

## Usage

The app starts in the system tray. Right-click the tray icon to access:

- **Settings** — configure corner, zone size, dwell delay, and startup behavior
- **About** — version info
- **Quit** — exit the application

### Settings

| Setting | Default | Description |
|---------|---------|-------------|
| Corner | Top-Left | Which screen corner triggers Task View |
| Zone size | 10 px | Size of the detection area in the corner |
| Dwell delay | 300 ms | How long the cursor must stay in the zone before triggering |
| Start with Windows | Off | Launch automatically at login |

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
│   └── NativeMethods.cs       # P/Invoke: WH_MOUSE_LL hook, SendInput
├── Core/
│   ├── HookManager.cs         # Global mouse hook (installs/uninstalls WH_MOUSE_LL)
│   ├── CornerDetector.cs      # Dwell state machine, drag suppression, multi-monitor
│   └── ActionTrigger.cs       # Sends Win+Tab via SendInput
├── Config/
│   ├── AppSettings.cs         # Settings model (corner, zone, delay, startup)
│   ├── ConfigManager.cs       # JSON persistence, SettingsChanged event
│   └── StartupManager.cs      # HKCU Run registry key
├── UI/
│   └── SettingsForm.cs        # Settings dialog
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

- Uses `WH_MOUSE_LL` (low-level global mouse hook) via P/Invoke — the only reliable way to detect mouse position across all applications
- Hook callback is kept minimal (coordinate read + bounds check only) to avoid Windows silently removing the hook due to timeout
- Hook delegate is pinned in a class field to prevent garbage collection
- Coordinates from the hook (`MSLLHOOKSTRUCT.pt`) are in physical pixels; `Screen.AllScreens` bounds match when Per-Monitor V2 DPI awareness is declared in the manifest
- `AppContext.BaseDirectory` is used for the settings file path (not `Assembly.Location`, which returns an empty string for single-file published executables)

## Known limitations

- When an elevated (admin) application is in the foreground, `SendInput` for Win+Tab may be blocked by UIPI. Task View is processed by the Windows shell (Explorer) at medium integrity, so in practice this rarely causes issues, but it is a Windows security boundary.
- Multiple simultaneous hot corners are not supported in v1.
