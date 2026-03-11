# Phase 3: Distribution - Research

**Researched:** 2026-03-11
**Domain:** .NET self-contained publishing + Inno Setup installer
**Confidence:** HIGH

## Summary

Phase 3 packages the completed WindowsHotSpot application for end-user distribution. There are two deliverables: (1) a self-contained single-file executable that requires no .NET runtime on the target machine, and (2) an Inno Setup installer (setup.exe) that installs without admin elevation. Both are well-documented, low-risk operations with established patterns.

The .NET SDK 10.0.103 is already installed on this machine. Inno Setup 6.7.1 is available via Chocolatey but is **not currently installed**. The plan must either install Inno Setup as a prerequisite step or provide the .iss script with manual instructions. The app currently has no .ico file -- Phase 1 used `SystemIcons.Application` as a fallback. A simple app icon should be added for the exe and installer.

**Primary recommendation:** Add publish properties to the csproj, create an Inno Setup .iss script with `PrivilegesRequired=lowest` and `DefaultDirName={localappdata}\WindowsHotSpot`, and provide a `build.ps1` script that runs both `dotnet publish` and ISCC.exe in sequence.

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| DIST-01 | App is published as a self-contained single executable (no .NET runtime required on target machine) | Self-contained single-file publish via `PublishSingleFile=true`, `SelfContained=true`, `RuntimeIdentifier=win-x64` in csproj. Verified against Microsoft Learn docs. |
| DIST-02 | Installer (Inno Setup) produces a single setup.exe that installs without requiring admin elevation | Inno Setup 6.x `PrivilegesRequired=lowest` with `DefaultDirName={localappdata}\WindowsHotSpot`. Available via `choco install innosetup -y`. |
</phase_requirements>

## Standard Stack

### Core
| Tool | Version | Purpose | Why Standard |
|------|---------|---------|--------------|
| dotnet publish | .NET SDK 10.0.103 | Self-contained single-file build | Built into SDK, official Microsoft approach |
| Inno Setup | 6.7.1 | Installer creation | Industry standard for simple Windows installers, free, single setup.exe output |

### Supporting
| Tool | Version | Purpose | When to Use |
|------|---------|---------|-------------|
| Chocolatey | 2.1.0 (installed) | Install Inno Setup | One-time prerequisite: `choco install innosetup -y` |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Inno Setup | WiX Toolset | WiX produces MSI (better for enterprise GPO), but far more complex scripting for a simple app |
| Inno Setup | NSIS | Similar capability, but Inno Setup has better documentation and simpler syntax |
| Single-file publish | Framework-dependent | Smaller output but requires .NET runtime on target -- unacceptable per DIST-01 |

## Architecture Patterns

### Build Output Structure
```
WindowsHotSpot/
├── installer/
│   └── WindowsHotSpot.iss       # Inno Setup script
├── build.ps1                     # Build + package script
└── WindowsHotSpot/
    ├── WindowsHotSpot.csproj     # (modified: add publish properties)
    └── Resources/
        └── app.ico               # Application icon
```

### Pattern 1: Publish Profile in csproj PropertyGroup

**What:** Add publish-related properties directly to the csproj rather than a separate publish profile XML. For a single-target app, inline properties are simpler.
**When to use:** When there is only one publish target (win-x64 self-contained).
**Example:**
```xml
<!-- Source: Microsoft Learn - Single file deployment -->
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net10.0-windows</TargetFramework>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <PublishSingleFile>true</PublishSingleFile>
  <SelfContained>true</SelfContained>
  <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  <ApplicationIcon>Resources\app.ico</ApplicationIcon>
</PropertyGroup>
```

**Key properties explained:**
- `PublishSingleFile` -- bundles all managed DLLs into a single exe
- `SelfContained` -- includes .NET runtime (no runtime install needed on target)
- `RuntimeIdentifier=win-x64` -- targets 64-bit Windows
- `EnableCompressionInSingleFile` -- compresses embedded assemblies, reduces exe size significantly (tradeoff: slightly slower cold start)
- `IncludeNativeLibrariesForSelfExtract` -- embeds native libraries into the single file (extracts to temp on first run); without this, native .dlls are loose files beside the exe
- `ApplicationIcon` -- sets the exe icon in Windows Explorer

### Pattern 2: Inno Setup Script for Per-User Install

**What:** Inno Setup .iss script that installs to `{localappdata}` without admin elevation.
**When to use:** For utilities that do not need machine-wide install or system services.
**Example:**
```iss
; Source: Inno Setup documentation
[Setup]
AppName=WindowsHotSpot
AppVersion=1.0.0
AppPublisher=WindowsHotSpot
DefaultDirName={localappdata}\WindowsHotSpot
DefaultGroupName=WindowsHotSpot
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=WindowsHotSpot-Setup
Compression=lzma2
SolidCompression=yes
SetupIconFile=..\WindowsHotSpot\Resources\app.ico
UninstallDisplayIcon={app}\WindowsHotSpot.exe
DisableProgramGroupPage=yes
ArchitecturesInstallIn64BitMode=x64compatible

[Files]
Source: "..\WindowsHotSpot\bin\Release\net10.0-windows\win-x64\publish\WindowsHotSpot.exe"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{userstartmenu}\WindowsHotSpot"; Filename: "{app}\WindowsHotSpot.exe"
Name: "{userdesktop}\WindowsHotSpot"; Filename: "{app}\WindowsHotSpot.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"

[UninstallDelete]
Type: files; Name: "{app}\settings.json"
```

**Key directives:**
- `PrivilegesRequired=lowest` -- no UAC prompt, no admin needed
- `DefaultDirName={localappdata}\WindowsHotSpot` -- per-user install directory (e.g., `C:\Users\<user>\AppData\Local\WindowsHotSpot`)
- `{userstartmenu}` instead of `{group}` -- Start Menu shortcut in user's menu, not all-users
- No registry entries in the installer -- the app's StartupManager already handles HKCU\Run
- `[UninstallDelete]` cleans up the settings.json that the app creates at runtime

### Pattern 3: Build Script

**What:** PowerShell script that runs dotnet publish followed by ISCC.exe.
**Example:**
```powershell
# build.ps1
$ErrorActionPreference = "Stop"

$publishDir = "WindowsHotSpot\bin\Release\net10.0-windows\win-x64\publish"

Write-Host "Publishing self-contained single-file..." -ForegroundColor Cyan
dotnet publish WindowsHotSpot\WindowsHotSpot.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed" }

Write-Host "Published to: $publishDir" -ForegroundColor Green
$exeSize = (Get-Item "$publishDir\WindowsHotSpot.exe").Length / 1MB
Write-Host "Exe size: $([math]::Round($exeSize, 1)) MB" -ForegroundColor Green

# Build installer (requires Inno Setup)
$iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
if (Test-Path $iscc) {
    Write-Host "Building installer..." -ForegroundColor Cyan
    & $iscc "installer\WindowsHotSpot.iss"
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed" }
    Write-Host "Installer created in dist\" -ForegroundColor Green
} else {
    Write-Host "Inno Setup not found. Install with: choco install innosetup -y" -ForegroundColor Yellow
    Write-Host "Skipping installer build." -ForegroundColor Yellow
}
```

### Anti-Patterns to Avoid
- **Using Assembly.Location in single-file apps:** Returns empty string. Use `AppContext.BaseDirectory` instead. (Already handled in Phase 2 per STATE.md decisions.)
- **Including RuntimeIdentifier without SelfContained:** In .NET 10, specifying RID no longer implies self-contained. Must set `SelfContained=true` explicitly.
- **PrivilegesRequired=admin for a tray utility:** Causes UAC prompt, annoying for users, unnecessary for a user-space app.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Installer | Custom installer exe | Inno Setup | Handles uninstall, Start Menu, upgrade detection, file versioning |
| Self-contained packaging | Manual file bundling | `dotnet publish` with PublishSingleFile | Handles native library extraction, compression, runtime bundling |
| Icon generation | Complex multi-resolution ICO from scratch | Simple 256x256 PNG converted to ICO | ICO format is straightforward; a single 256x256 image works for all contexts |

## Common Pitfalls

### Pitfall 1: Missing RuntimeIdentifier Causes Framework-Dependent Output
**What goes wrong:** `dotnet publish` without `-r win-x64` or `<RuntimeIdentifier>` produces a framework-dependent build even with `PublishSingleFile=true`.
**Why it happens:** .NET 10 no longer infers self-contained from the presence of PublishSingleFile alone.
**How to avoid:** Always specify `RuntimeIdentifier=win-x64` in the csproj or on the command line.
**Warning signs:** Published output contains many .dll files instead of a single .exe.

### Pitfall 2: IncludeNativeLibrariesForSelfExtract Omitted
**What goes wrong:** Native .dll files (e.g., from the .NET runtime) appear as loose files alongside the exe instead of being bundled.
**Why it happens:** By default, only managed assemblies are bundled. Native libraries are excluded.
**How to avoid:** Set `<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>`. This causes native libs to be extracted to a temp directory on first run.
**Warning signs:** Multiple .dll files in the publish directory alongside the .exe.

### Pitfall 3: Inno Setup Uninstall Does Not Remove Runtime-Created Files
**What goes wrong:** After uninstall, settings.json (and the HKCU\Run registry key if startup was enabled) remain.
**Why it happens:** Inno Setup only uninstalls files it installed. Settings.json is created at runtime by the app.
**How to avoid:** Add `[UninstallDelete]` entries for known runtime files. For registry, add `[UninstallRun]` or `[Registry]` to clean the Run key, OR accept that the app's StartupManager writes/removes it and rely on uninstall removing the exe (so the Run key becomes a harmless orphan pointing to a nonexistent file).
**Warning signs:** Leftover files/folders after uninstall.

### Pitfall 4: Inno Setup Path to Published Exe Wrong
**What goes wrong:** The .iss script `[Files]` Source path doesn't match the actual publish output location.
**Why it happens:** The default publish output path depends on the project configuration and framework version.
**How to avoid:** The path is `bin\Release\net10.0-windows\win-x64\publish\WindowsHotSpot.exe` when using `RuntimeIdentifier=win-x64` in the csproj. Verify with a test publish.
**Warning signs:** ISCC.exe compilation fails with "file not found".

### Pitfall 5: Large Self-Contained Exe Size
**What goes wrong:** The self-contained single-file exe is 60-80+ MB.
**Why it happens:** The entire .NET runtime and WinForms framework libraries are bundled.
**How to avoid:** Use `EnableCompressionInSingleFile=true` to reduce size (typically 30-40% reduction). Optionally enable trimming (`PublishTrimmed=true`) but this is risky for WinForms (reflection-heavy). Compression is the safer choice.
**Warning signs:** Exe over 80 MB without compression.

## Code Examples

### Exact dotnet publish Command
```bash
# Source: Microsoft Learn - dotnet publish
dotnet publish WindowsHotSpot/WindowsHotSpot.csproj -c Release
```
With the publish properties in the csproj, this is all that's needed. The RuntimeIdentifier, PublishSingleFile, and SelfContained are read from the project file.

Output location: `WindowsHotSpot/bin/Release/net10.0-windows/win-x64/publish/WindowsHotSpot.exe`

### csproj Additions (Exact Properties to Add)
```xml
<!-- Add to existing PropertyGroup in WindowsHotSpot.csproj -->
<RuntimeIdentifier>win-x64</RuntimeIdentifier>
<PublishSingleFile>true</PublishSingleFile>
<SelfContained>true</SelfContained>
<EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
<IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
<ApplicationIcon>Resources\app.ico</ApplicationIcon>
```

### Application Icon in csproj
```xml
<!-- ApplicationIcon sets the exe icon visible in Explorer -->
<ApplicationIcon>Resources\app.ico</ApplicationIcon>
```
The .ico file must contain at least 16x16, 32x32, and 256x256 sizes. For a simple tray utility, a basic icon is sufficient. The icon can be created from a 256x256 PNG using any ICO converter tool, or generated programmatically.

### Inno Setup Install Command
```bash
# Install Inno Setup via Chocolatey (one-time, requires admin)
choco install innosetup -y

# Compile the installer script
"C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\WindowsHotSpot.iss
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Framework-dependent + runtime installer | Self-contained single-file | .NET Core 3.0+ (refined through .NET 8+) | No runtime prerequisite for end users |
| Assembly.Location for file paths | AppContext.BaseDirectory | .NET 5+ single-file | Assembly.Location returns empty string in single-file apps |
| RID implies SelfContained | Explicit SelfContained required | .NET 8+ | Must explicitly set `<SelfContained>true</SelfContained>` |
| WiX for simple installers | Inno Setup for per-user utilities | Always (for non-enterprise) | WiX is overkill for a single-exe tray utility |

## Open Questions

1. **App Icon Source**
   - What we know: No .ico file exists. Phase 1 used `SystemIcons.Application` for the tray icon.
   - What's unclear: Whether the user wants a custom-designed icon or a simple placeholder.
   - Recommendation: Create a simple icon (e.g., a hot corner symbol) as a 256x256 PNG, convert to ICO. The tray icon (`NotifyIcon.Icon`) should also be updated to use the embedded icon from the exe resources instead of `SystemIcons.Application`.

2. **Inno Setup Installation**
   - What we know: Inno Setup 6.7.1 is available via `choco install innosetup -y` but is not installed on this machine.
   - What's unclear: Whether the plan should install it automatically or just provide the script.
   - Recommendation: The build.ps1 script should check for ISCC.exe and print a helpful message if missing. The plan should include a task to install Inno Setup via Chocolatey as a prerequisite.

3. **Trimming**
   - What we know: `PublishTrimmed=true` can reduce exe size significantly but is risky for WinForms (reflection-heavy).
   - What's unclear: Whether the WinForms trimming warnings are blocking for this app.
   - Recommendation: Do NOT enable trimming for v1. Compression alone provides adequate size reduction. Trimming can be investigated in v2 if size is a concern.

## Sources

### Primary (HIGH confidence)
- [Microsoft Learn - Single file deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/single-file/overview) - PublishSingleFile properties, API incompatibilities, compression, native library extraction
- [Microsoft Learn - dotnet publish](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-publish) - CLI options, RuntimeIdentifier, SelfContained behavior in .NET 10
- Local verification: `dotnet --version` confirms SDK 10.0.103 installed
- Local verification: `choco search innosetup` confirms 6.7.1 available

### Secondary (MEDIUM confidence)
- [Inno Setup documentation](https://jrsoftware.org/ishelp/) - PrivilegesRequired=lowest, DefaultDirName constants, [Files] section syntax
- Project domain research (SUMMARY.md) - Inno Setup recommendation, self-contained publish approach

### Tertiary (LOW confidence)
- None

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH - dotnet publish and Inno Setup are well-documented, SDK verified locally
- Architecture: HIGH - standard publish properties verified against Microsoft Learn docs
- Pitfalls: HIGH - Assembly.Location, native library extraction, and RID behavior documented in official sources

**Research date:** 2026-03-11
**Valid until:** 2026-04-11 (stable domain, unlikely to change)
