# Milestones

## v1.2 Per-Corner Actions & Multi-Monitor (Shipped: 2026-03-18)

**Phases completed:** 3 phases (02–04), 11 plans
**Git range:** feat(02-01) → fix(04-cp)
**Timeline:** 2026-03-11 → 2026-03-18 (7 days)
**LOC:** ~1,963 C# source lines

**Key accomplishments:**
1. Per-corner action model: `CornerAction` enum (Disabled/TaskView/ShowDesktop/ActionCenter/Custom) with per-monitor config keyed by `Screen.DeviceName`
2. Automatic v1.x → v1.2 settings migration using `[JsonPropertyName]` nullable hook — zero data loss
3. `CornerRouter` — per-monitor detector pool rebuilt on settings changes and `WM_DISPLAYCHANGE`
4. Live monitor hot-plug support: adding/removing a monitor updates corners immediately without restart
5. `KeyRecorderPanel` with `WH_KEYBOARD_LL` for Win-key capture; rejects bare alphanumerics; accepts modifier combos
6. Redesigned `SettingsForm`: 2×2 corner grid + monitor selector + Same-on-all-monitors toggle

---

## v1.1 Single-Instance Guard (Shipped: 2026-03-17)

**Phases completed:** 1 phase, 2 plans

**Key accomplishments:**
- Single-instance guard via `Local\` mutex; second launch signals first instance via `WM_COPYDATA` to show Settings, then exits silently
- IPC via hidden `HWND_MESSAGE` `NativeWindow` — invisible to Alt+Tab, reliable `SendMessage` delivery

---
