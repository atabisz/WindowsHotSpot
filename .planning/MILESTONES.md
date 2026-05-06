# Milestones

## v1.5 Window Interactions (Shipped: 2026-05-06)

**Phases completed:** 3 phases (08–10), 5 plans
**Git range:** `23cb1a8` → `5b02d60`
**Timeline:** 2026-05-06 (single session)
**LOC:** +437 net, 2,837 total C# source LOC

**Key accomplishments:**
1. `HookManager` gains `MouseWheeled (Action<int,Point>?)` and `MouseDoubleClicked (Action<Point>?)` events; `WheelSuppressionPredicate` for consumer-gated wheel suppression
2. `ScrollResizeHandler` — Ctrl+Alt+scroll resizes any window symmetrically around cursor, clamped to `SM_CXMINTRACK`/`SM_CYMINTRACK` and `Screen.WorkingArea` per-edge
3. "Window Interactions" section in SettingsForm with configurable scroll resize step (default 20 px/notch)
4. `AlwaysOnTopHandler` — Ctrl+Alt+double-click toggles `WS_EX_TOPMOST` via `SetWindowPos(HWND_TOPMOST/HWND_NOTOPMOST)`; tray balloon uses target window title
5. All three Ctrl+Alt gestures (drag, resize, AOT) share AltGr guard, maximized skip, and UIPI elevated-window skip
6. Post-ship: console font zoom suppressed; screen/taskbar edge clamping corrected

---

## v1.4 Window Drag Anywhere (Shipped: 2026-05-05)

**Phases completed:** 3 phases (05–07), 6 plans
**Git range:** `f7e96f3` (feat(05-01)) → `7ae45d8` (fix: self-heal)
**Timeline:** 2026-05-05 (single session)
**LOC:** +449 / -15 across 6 `.cs` files

**Key accomplishments:**
1. `HookManager` gains `SuppressionPredicate` API — consumers swallow `WM_LBUTTONDOWN/UP` without modifying hook internals
2. `WindowDragHandler.cs` (~260 LOC) — LCtrl+LAlt drag with AltGr guard, maximized-window skip, real-time absolute-delta tracking, cursor feedback
3. `WindowDragPassThrough` setting — controls click pass-through on non-draggable surfaces
4. Elevated window detection — UIPI-blocked targets skipped cleanly; clicks pass through normally
5. SAS self-heal — `GetKeyState` reconciliation prevents lockup after Ctrl+Alt+Del
6. `WindowDragHandler` promoted to permanent `readonly` field in `HotSpotApplicationContext`

---

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
