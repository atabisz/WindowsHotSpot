# Feature Landscape

**Domain:** Windows hot corner / system tray utility
**Researched:** 2026-03-11
**Confidence:** HIGH (based on analysis of 6 competing open-source projects)

## Competitors Analyzed

| Project | Stars | Language | Notable Trait |
|---------|-------|----------|---------------|
| WinXCorners (vhanla) | 907 | Delphi | Most feature-rich, all 4 corners, custom hotkey scripting |
| HotCornersWin (flexits) | 127 | C# .NET 9 | Best UX polish, MSI installer, multi-monitor modes |
| taviso/hotcorner | 407 | C | Ultra-minimal, no config file, edit-and-recompile |
| HotCorners (osmanonurkoc) | 21 | C# .NET 6 | Modern UI, theme-aware, single-file publish |
| HotFrameFx (kruizer23) | 22 | Unknown | Edges AND corners, keystroke or app launch actions |
| timrobertsdev/hotcorners | 33 | Rust | Minimal, no GUI yet, hard-coded config |

## Table Stakes

Features users expect. Missing = product feels incomplete.

| Feature | Why Expected | Complexity | Notes |
|---------|--------------|------------|-------|
| Corner detection triggers Task View | Core value proposition; every competitor does this | Med | Requires global low-level mouse hook (WH_MOUSE_LL) or polling. Hook is standard approach. |
| System tray icon (no taskbar button) | Every competitor runs in tray. Users expect background utility behavior. | Low | WinForms NotifyIcon, FormBorderStyle.None, ShowInTaskbar=false |
| Configurable corner selection | 5 of 6 competitors offer this. Users want to pick which corner. | Low | Dropdown or radio buttons in settings; TL/TR/BL/BR |
| Configurable dwell delay | All competitors with GUI have this. Prevents accidental triggers. | Low | Timer that resets when cursor leaves zone. Default 150-300ms is standard. |
| Configurable zone size (sensitivity) | 4 of 6 competitors expose this. Power users need to tune it. | Low | Pixel radius from corner. Defaults vary: 2-20px. 5-10px is common. |
| Start with Windows option | Every competitor supports this. Users expect persistence. | Low | HKCU Run key or Startup folder shortcut. Registry is cleaner. |
| Settings persistence across restarts | All competitors persist config. JSON file or registry. | Low | JSON file next to exe per PROJECT.md. Simple and inspectable. |
| Settings UI (not edit-and-recompile) | taviso's "edit source to configure" approach is its main criticism. Users expect a GUI. | Med | WinForms dialog. Does not need to be fancy. |
| Ignore mouse-down / dragging | WinXCorners and HotCornersWin both do this. Without it, drag-to-corner triggers accidentally. | Low | Check if any mouse button is held during hook callback. Critical for usability. |
| Quit option in tray menu | Universal. Users must be able to exit. | Low | ContextMenuStrip on NotifyIcon |
| Multi-monitor awareness | HotCornersWin and WinXCorners support this. Users with multiple monitors will file bugs immediately if only primary works. | Med | Use Screen.AllScreens to find corner coordinates. Must handle monitor arrangement (non-rectangular layouts). |

## Differentiators

Features that set product apart. Not expected, but valued.

| Feature | Value Proposition | Complexity | Notes |
|---------|-------------------|------------|-------|
| Full-screen app detection (auto-disable) | Prevents triggering during games/video. HotCornersWin and WinXCorners have it. Most users playing games will be frustrated without this. | Med | Check foreground window bounds vs screen bounds. SHQueryUserNotificationState API is another approach. |
| Visual countdown indicator | WinXCorners shows a visible countdown before triggering. Gives user feedback and chance to abort. | Med | Small overlay window at corner that fills/fades during dwell. Nice but not essential for v1. |
| Dark/light theme matching | WinXCorners, HotCornersWin, and osmanonurkoc all do this. Feels native on Win10/11. | Med | Read HKCU registry for AppsUseLightTheme. WinForms dark mode is experimental until .NET 10. Manual styling is more reliable. |
| Quick enable/disable toggle | HotCornersWin: single click on tray icon toggles on/off. WinXCorners has a switch in popup. Useful for temporary disable. | Low | Tray icon click toggles; change icon to indicate state (gray = disabled). |
| Multiple corner actions (all 4 corners) | WinXCorners, HotCornersWin, HotFrameFx, osmanonurkoc all support this. PROJECT.md explicitly scopes this out, but it is the strongest differentiator across competitors. | Med | Only increases config complexity. Detection logic is the same per-corner. |
| Edge triggers (not just corners) | HotFrameFx supports screen edges in addition to corners. Unique differentiator. | High | Much larger trigger zones, more complex geometry, higher false-positive risk. |
| Custom action (run exe / command) | WinXCorners, HotCornersWin, osmanonurkoc all allow arbitrary commands. | Med | Process.Start with configurable path + args. Security consideration: user controls what runs. |
| Multiple action types (not just Task View) | All mature competitors offer Show Desktop, Lock Screen, Action Center, Start Menu, etc. | Low-Med | Each action is just a different SendInput sequence or API call. Adding more is cheap once the framework exists. |
| Custom hotkey scripting | WinXCorners v1.3.1 has a mini-DSL for conditional hotkey sequences. Very power-user. | High | Niche. Impressive but over-engineered for most users. |
| MSI/installer with proper uninstall | HotCornersWin ships an MSI. Professional feel. | Med | WiX or NSIS per PROJECT.md. Users expect Add/Remove Programs entry. |

## Anti-Features

Features to explicitly NOT build.

| Anti-Feature | Why Avoid | What to Do Instead |
|--------------|-----------|-------------------|
| Edit-and-recompile configuration | taviso's approach is the #1 complaint. Eliminates non-developer users entirely. | Ship a settings dialog. Even a basic WinForms form is sufficient. |
| Per-monitor corner configuration | PROJECT.md explicitly excludes this. Adds complexity for niche benefit. One global corner setting covers 95% of users. | Single global corner setting applied to all monitors. |
| Hotkey scripting DSL | WinXCorners' conditional hotkey language is impressive but a maintenance burden and confuses most users. | If custom actions are added later, use a simple "run this exe" approach. |
| Polling-based cursor detection | HotCornersWin polls every 75ms. Works but wastes CPU and misses fast movements. taviso's hook approach is better. | Use WH_MOUSE_LL global hook. Zero polling, event-driven, catches every movement. |
| Admin/elevation requirement | WinXCorners notes that elevated apps block detection without admin. Do not require admin to install or run. | Run as standard user. Document the UIPI limitation (elevated foreground apps may block hooks). Accept this tradeoff. |
| Multiple simultaneous hot corners (v1) | PROJECT.md scopes this out. Keep v1 simple with single corner. | Single configurable corner. Can revisit in v2 if demand exists. |
| Configurable trigger action (v1) | PROJECT.md scopes to Task View only. Keeps v1 focused. | Always trigger Win+Tab. Action configurability is a natural v2 feature. |

## Feature Dependencies

```
Global mouse hook --> Corner detection --> Dwell timer --> Action trigger (Task View)
                                       --> Drag detection (suppress during drag)
                                       --> Full-screen detection (suppress during fullscreen)

Settings UI --> Config persistence (JSON) --> Corner selection
                                          --> Zone size
                                          --> Dwell delay
                                          --> Start with Windows (registry)

System tray icon --> Context menu (Quit, About, Settings)
                 --> Enable/disable toggle (click to toggle)

Multi-monitor --> Screen enumeration --> Corner coordinate calculation
```

Core dependency chain: Hook -> Detection -> Timer -> Action. Everything else layers on top.

## MVP Recommendation

Prioritize (in build order):

1. **Global mouse hook + corner detection + dwell timer + Task View trigger** -- the core loop. Without this, nothing else matters.
2. **System tray icon with Quit menu** -- users need a way to exit. Minimum viable tray presence.
3. **Drag suppression** -- without this, accidental triggers will frustrate users immediately. Must ship in v1.
4. **Settings dialog (corner, zone size, dwell delay)** -- table stakes per competitor analysis.
5. **JSON config persistence** -- settings must survive restarts.
6. **Start with Windows toggle** -- registry run key. Users expect it.
7. **Multi-monitor support** -- too many users have multi-monitor setups to skip this.
8. **Installer (NSIS/WiX)** -- needed for distribution.

Defer to v2:
- **Full-screen detection**: Valuable but not blocking. Users can temporarily quit the app during gaming. Medium complexity.
- **Visual countdown**: Nice UX polish, not essential.
- **Enable/disable toggle via tray click**: Low effort, could sneak into v1 if time permits.
- **Multiple corners / multiple actions**: Explicitly out of scope per PROJECT.md.
- **Dark/light theme**: WinForms theming is experimental. Manual styling is effort for cosmetic benefit.

## Sources

- GitHub: vhanla/winxcorners (907 stars, Delphi, v1.3.2 2024) -- most feature-complete competitor
- GitHub: flexits/HotCornersWin (127 stars, C# .NET 9, MSI installer) -- closest tech stack match
- GitHub: taviso/hotcorner (407 stars, C, ultra-minimal) -- anti-pattern for UX, gold standard for minimal overhead
- GitHub: osmanonurkoc/HotCorners (21 stars, C# .NET 6) -- modern C# reference implementation
- GitHub: kruizer23/hotframefx (22 stars, edge+corner support) -- unique edge trigger feature
- GitHub: timrobertsdev/hotcorners (33 stars, Rust, minimal) -- shows MVP feature set expectations
