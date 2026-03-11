# Milestones

## v1.0 MVP (Shipped: 2026-03-11)

**Phases completed:** 3 phases, 6 plans, 0 tasks

**Stats:** 831 LOC C# (17 source files), 14 project files total

**Key accomplishments:**
- Global WH_MOUSE_LL hook with 3-state dwell machine (Idle/Dwelling/Triggered), drag suppression, and multi-monitor support via Per-Monitor V2 DPI manifest
- Fixed critical P/Invoke bug: InputUnion missing MOUSEINPUT caused Marshal.SizeOf<INPUT>=28 (not 40), making SendInput silently return 0; adding MOUSEINPUT fixed Win+Tab delivery
- System tray ApplicationContext (no taskbar button) with Settings/About/Quit menu wired end-to-end
- JSON config persistence with live settings propagation via SettingsChanged event — no app restart required
- Start with Windows via HKCU Run registry key with real-time enable/disable toggle
- Self-contained single-file publish + Inno Setup 6 installer producing 45 MB setup.exe without admin elevation

---

