# System Tray Icon Design — WindowsHotSpot

**Date:** 2026-03-11
**Status:** Approved

## Summary

Replace the `SystemIcons.Application` fallback with a custom multi-size `.ico` file that communicates the app's purpose: mouse cursor dwelling in a screen corner to trigger Task View.

## Visual Design

**Concept:** Cursor + Task View (Option B)
A white mouse cursor points into an amber-glowing top-left corner zone, with a 2×2 grid of blue mini-windows in the bottom-right hinting at Task View.

### Colour Palette

| Element | Colour | Usage |
|---|---|---|
| Corner glow | `#E8A020` (amber) | Hot zone fill + accent circle |
| Cursor | `#FFFFFF` with `#111111` outline | Arrow pointer |
| Mini-windows | `#4A90D9` (Windows blue) | 2×2 Task View grid |
| Background | Transparent | Native light/dark tray compatibility |

### Sizes

| Size | Context | Notes |
|---|---|---|
| 16×16 | System tray (primary) | Simplified — cursor + corner glow + 2×2 mini-windows (3 px each) |
| 32×32 | Alt+Tab, high-DPI tray | Full design with amber accent dot at corner |
| 48×48 | File Explorer, About dialog | Full detail — larger accent circle, crisp mini-windows |

### Per-size layout (origin = top-left)

**16×16**
- Corner glow: 6×6 rect at (0,0), amber, 35% opacity
- Cursor: polygon `1,1 → 1,8 → 3,6 → 5,10 → 6,9 → 4,5 → 7,5`, white fill, black 0.5px stroke
- Mini-windows: 4 rects (3×2 px each) in 2×2 grid at (9,9), blue

**32×32**
- Corner glow: 12×12 rect at (0,0), amber, 30% opacity; amber circle r=3 at (4,4), 50% opacity
- Cursor: polygon scaled ×2, white fill, `#111` 1px stroke
- Mini-windows: 4 rects (6×5 px each) in 2×2 grid at (18,18), blue

**48×48**
- Corner glow: 18×18 rect at (0,0), amber, 25% opacity; outer amber circle r=5, inner r=2.5 at (6,6)
- Cursor: polygon scaled ×3, white fill, `#111` 1.5px stroke
- Mini-windows: 4 rects (9×7 px each) in 2×2 grid at (27,27), blue

## Implementation

### Approach

Write a C# icon generator (`tools/GenerateIcon.cs`) that:
1. Renders each size to a `Bitmap` using `System.Drawing.Graphics`
2. Saves each bitmap as a PNG byte array in memory
3. Manually encodes a valid ICO binary (ICONDIR + ICONDIRENTRY headers + PNG frames)
4. Writes to `WindowsHotSpot/Resources/app.ico`

The generator is a standalone `dotnet-script` / top-level-statements `.cs` file run once. The output `app.ico` is committed to source control.

### Wiring

Update `HotSpotApplicationContext` to load the embedded resource instead of `SystemIcons.Application`:

```csharp
_trayIcon = new NotifyIcon
{
    Icon = new Icon(typeof(HotSpotApplicationContext), "Resources/app.ico"),
    ...
};
```

Update `WindowsHotSpot.csproj` to embed `app.ico` as `EmbeddedResource`.

### ICO Binary Format

```
ICONDIR   (6 bytes): reserved=0, type=1, count=3
ICONDIRENTRY × 3   (16 bytes each): width, height, colorCount=0, reserved=0, planes=1, bitCount=32, bytesInRes, imageOffset
PNG frame data × 3 (variable)
```

Windows Vista+ supports PNG-compressed frames in ICO files, so we embed raw PNG bytes directly — no BMP conversion needed.

## Files Changed

| File | Change |
|---|---|
| `tools/GenerateIcon.cs` | New — renders icon and writes ICO |
| `WindowsHotSpot/Resources/app.ico` | Updated — replaces empty placeholder |
| `WindowsHotSpot/WindowsHotSpot.csproj` | Add `EmbeddedResource` for app.ico |
| `WindowsHotSpot/HotSpotApplicationContext.cs` | Load icon from embedded resource |
