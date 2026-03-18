# Phase 2: Config Foundation - Research

**Researched:** 2026-03-17
**Domain:** C# .NET 10 / System.Text.Json — data model evolution, JSON migration, SendInput dispatch
**Confidence:** HIGH

---

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|-----------------|
| CONF-01 | Existing v1.x settings.json is migrated to the new schema without data loss | Migration pattern: `JsonSerializer.Deserialize` with `[JsonIgnore(Condition = WhenWritingDefault)]` tolerates unknown/missing keys; v1 `Corner` field maps to new `CornerActions` entry for that corner |
| CRNA-01 | Each corner can be independently configured with an action or set to disabled | New `CornerAction` enum + `Dictionary<HotCorner, CornerAction>` keyed by the four corners |
| CRNA-02 | User can assign Win+Tab (Task View) to any corner | `CornerAction.TaskView` value; existing `ActionTrigger.SendTaskView()` is the dispatch target |
| CRNA-03 | User can assign Show Desktop (Win+D) to any corner | `CornerAction.ShowDesktop` value; Win+D = VK_LWIN down/up + VK_D down/up via SendInput |
| CRNA-04 | User can assign Action Center (Win+A) to any corner | `CornerAction.ActionCenter` value; Win+A = VK_LWIN down/up + VK_A down/up via SendInput |
| CRNA-06 | A corner set to Disabled triggers no action when dwelled | `ActionDispatcher.Dispatch(CornerAction.Disabled)` is a no-op; CornerDetector checks before firing |
</phase_requirements>

---

## Summary

Phase 2 transforms WindowsHotSpot from a single-corner, single-action app into a per-corner configurable app. The work is entirely in-process C# with no new NuGet dependencies — the project already uses `System.Text.Json`, `System.Windows.Forms`, and P/Invoke, all of which remain the right tools.

The core deliverables are: (1) a `CornerAction` enum with four values (TaskView, ShowDesktop, ActionCenter, Disabled); (2) an updated `AppSettings` that holds a `Dictionary<HotCorner, CornerAction>` instead of a single `Corner` property; (3) a `ConfigManager.Load()` migration path that reads the v1.x flat structure and promotes it into the new schema; and (4) an `ActionDispatcher` static class that translates a `CornerAction` value into the correct `SendInput` call.

The single trickiest area is the migration: the v1.x JSON has `"Corner": "TopLeft"` (one active corner) while the v2 schema needs four corner entries. Migration must read the old field if present and set the corresponding corner to `CornerAction.TaskView`, leaving the other three as `Disabled`. The existing `System.Text.Json` deserializer handles unknown/missing properties gracefully by default (it ignores them), so a two-pass load pattern works cleanly.

**Primary recommendation:** Add `CornerAction` enum + `CornerActions` dictionary to `AppSettings`, write an explicit migration method in `ConfigManager`, and introduce `ActionDispatcher` as a static dispatch class that replaces the direct `ActionTrigger.SendTaskView()` call in `CornerDetector`.

---

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| System.Text.Json | In-box (.NET 10) | JSON serialization/deserialization of AppSettings | Already used; no new dependency needed |
| System.Windows.Forms | In-box (.NET 10-windows) | WinForms Timer, UI thread safety | Already used; SendInput must stay on UI thread |
| user32.dll P/Invoke | Win32 | SendInput for Win+D, Win+A keystrokes | Already established in NativeMethods.cs |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| JsonStringEnumConverter | In-box System.Text.Json | Serialize enums as strings in JSON | Already applied to HotCorner; apply same to CornerAction |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| System.Text.Json | Newtonsoft.Json | Newtonsoft has more migration helpers but adds a dependency; STJ is sufficient for this schema |
| Dictionary<HotCorner, CornerAction> | Four individual properties (TopLeftAction, etc.) | Four properties are more verbose and harder to iterate; dictionary is cleaner for Phase 3's per-monitor loop |

**Installation:** No new packages required. All types are in-box.

---

## Architecture Patterns

### Recommended Project Structure

No new folders required. Changes spread across existing locations:

```
WindowsHotSpot/
├── Config/
│   ├── AppSettings.cs         # Add CornerAction enum + CornerActions dict
│   └── ConfigManager.cs       # Add MigrateV1() helper called from Load()
├── Core/
│   ├── ActionDispatcher.cs    # NEW: routes CornerAction -> SendInput call
│   └── CornerDetector.cs      # Change OnDwellComplete: call ActionDispatcher, not ActionTrigger directly
└── Native/
    └── NativeMethods.cs       # Add VK_D (0x44) and VK_A (0x41) constants
```

### Pattern 1: Per-Corner Action Dictionary in AppSettings

**What:** Replace the single `Corner` field with a `Dictionary<HotCorner, CornerAction>` that maps each of the four corners to its assigned action. The dictionary is always fully populated (all four corners present) after load/migration, so callers never need a null check.

**When to use:** Any time the app needs to know what action a specific corner should fire.

**Example:**
```csharp
// Source: project codebase pattern — System.Text.Json handles Dictionary<TKey,TValue>
// with JsonStringEnumConverter for both key and value.

[JsonConverter(typeof(JsonStringEnumConverter))]
internal enum CornerAction { Disabled, TaskView, ShowDesktop, ActionCenter }

internal sealed class AppSettings
{
    // v1.x legacy field — present only in migrated reads; not written by v2
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public HotCorner? LegacyCorner { get; set; }  // nullable; null = no v1 data

    public Dictionary<HotCorner, CornerAction> CornerActions { get; set; } = DefaultCornerActions();
    public int ZoneSize { get; set; } = 10;
    public int DwellDelayMs { get; set; } = 150;
    public bool StartWithWindows { get; set; } = false;

    public static Dictionary<HotCorner, CornerAction> DefaultCornerActions() =>
        new()
        {
            [HotCorner.TopLeft]     = CornerAction.Disabled,
            [HotCorner.TopRight]    = CornerAction.Disabled,
            [HotCorner.BottomLeft]  = CornerAction.Disabled,
            [HotCorner.BottomRight] = CornerAction.Disabled,
        };
}
```

### Pattern 2: Two-Pass V1 Migration in ConfigManager.Load()

**What:** Deserialize the JSON into the new `AppSettings` type (which tolerates unknown keys by default in STJ). If the resulting `CornerActions` dictionary is empty/default AND the legacy file had a `Corner` property, that property will be missing from the new struct but can be detected by a second targeted parse.

**Better approach:** Make the v1 `Corner` field deserialize into a nullable `HotCorner? LegacyCorner` property on `AppSettings`. After deserialization, if `LegacyCorner` is non-null and `CornerActions` is all-Disabled (default), promote: set `CornerActions[LegacyCorner.Value] = CornerAction.TaskView`, then clear `LegacyCorner`. Save immediately so the migrated file is written in v2 format.

**When to use:** On every Load() call; the migration is idempotent (LegacyCorner will be null after first save).

**Example:**
```csharp
// Source: project pattern + System.Text.Json default unknown-property behavior
public void Load()
{
    if (File.Exists(SettingsPath))
    {
        try
        {
            string json = File.ReadAllText(SettingsPath);
            AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (loaded is not null)
            {
                Settings = loaded;
                MigrateV1();   // no-op if already v2
            }
        }
        catch
        {
            Settings = new AppSettings();
        }
    }
    if (Settings.StartWithWindows)
        StartupManager.RefreshPath();
}

private void MigrateV1()
{
    // LegacyCorner is non-null only when old "Corner" field was present in JSON
    // AND CornerActions is still all-Disabled (i.e., not already set by a v2 file).
    bool allDisabled = Settings.CornerActions.Values.All(a => a == CornerAction.Disabled);
    if (Settings.LegacyCorner is HotCorner legacy && allDisabled)
    {
        Settings.CornerActions[legacy] = CornerAction.TaskView;
        Settings.LegacyCorner = null;
        Save();   // persist migrated file immediately
    }
}
```

**Note:** STJ ignores JSON properties that don't map to C# properties by default (`JsonUnknownTypeHandling` default is `JsonUnknownTypeHandling.Skip`). The v1 `"Corner"` field maps cleanly to `LegacyCorner` because we explicitly added the property with `[JsonPropertyName("Corner")]` or by naming it to match. Use `[JsonPropertyName("Corner")]` on `LegacyCorner` to capture the v1 field name.

### Pattern 3: ActionDispatcher Static Class

**What:** A static class that takes a `CornerAction` value and fires the correct `SendInput` sequence. Replaces the direct `ActionTrigger.SendTaskView()` call in `CornerDetector.OnDwellComplete`.

**When to use:** Called from `CornerDetector.OnDwellComplete` (on UI thread via WinForms Timer — safe for SendInput).

**Example:**
```csharp
// Source: project NativeMethods.cs pattern (existing VK constants + SendInput)
internal static class ActionDispatcher
{
    public static void Dispatch(CornerAction action)
    {
        switch (action)
        {
            case CornerAction.TaskView:
                ActionTrigger.SendTaskView();          // reuse existing method
                break;
            case CornerAction.ShowDesktop:
                SendWinKey(NativeMethods.VK_D);        // Win+D
                break;
            case CornerAction.ActionCenter:
                SendWinKey(NativeMethods.VK_A);        // Win+A
                break;
            case CornerAction.Disabled:
                break;                                 // no-op
        }
    }

    private static void SendWinKey(ushort vk)
    {
        var inputs = new NativeMethods.INPUT[4];
        inputs[0] = MakeKeyInput(NativeMethods.VK_LWIN, keyUp: false);
        inputs[1] = MakeKeyInput(vk,                    keyUp: false);
        inputs[2] = MakeKeyInput(vk,                    keyUp: true);
        inputs[3] = MakeKeyInput(NativeMethods.VK_LWIN, keyUp: true);
        NativeMethods.SendInput((uint)inputs.Length, inputs,
            Marshal.SizeOf<NativeMethods.INPUT>());
    }

    // ... MakeKeyInput identical to ActionTrigger's private version
}
```

### Pattern 4: CornerDetector Wired to a Specific Corner's Action

**What:** In Phase 2, `CornerDetector` still handles a single corner (Phase 3 adds multiple detectors). Its `OnDwellComplete` must now look up the corner's action from `AppSettings.CornerActions` and call `ActionDispatcher.Dispatch()`.

**Example:**
```csharp
private void OnDwellComplete(object? sender, EventArgs e)
{
    _dwellTimer.Stop();
    ActionDispatcher.Dispatch(_configManager.Settings.CornerActions[_activeCorner]);
    _state = DetectorState.Triggered;
}
```

`UpdateSettings` must also accept the new dictionary rather than individual fields. Or `CornerDetector` can hold a reference to `AppSettings` directly (simpler for Phase 2, easier for Phase 3 to extend).

### Anti-Patterns to Avoid

- **Storing CornerAction as int in JSON:** Use `JsonStringEnumConverter` consistently so JSON is human-readable and round-trips safely. The existing `HotCorner` enum already demonstrates this correctly.
- **Calling ActionDispatcher from hook callback:** `OnDwellComplete` fires on the UI thread (WinForms Timer). `OnMouseMoved` fires from the hook callback, also on the UI thread in this app — but the pattern is that only Timer ticks call dispatch. Keep it that way.
- **Partial dictionary:** Always ensure all four `HotCorner` keys are present in `CornerActions`. If deserialization produces a partial dictionary (e.g., from a hand-edited JSON), fill missing keys with `Disabled` during `Load()`.
- **Saving during migration while STJ options include `LegacyCorner`:** After migration and Save(), `LegacyCorner` (null) is written as nothing because of `[JsonIgnore(Condition = WhenWritingDefault)]` — so the v1 `"Corner"` key disappears from the saved file. This is correct and intentional.

---

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| JSON schema versioning | Custom version-field parser | STJ nullable property + conditional migration | STJ handles missing/unknown fields by default; nullable property is simpler than version integers |
| Win+D / Win+A key sequences | Custom key-sequence abstraction | Reuse existing `MakeKeyInput` + `SendInput` pattern from `ActionTrigger` | The cbSize pitfall is already solved; reuse avoids re-introducing it |
| VK code lookup | String-to-VK dictionary | Hardcoded constants in NativeMethods.cs | Phase 2 only needs 2 new VKs (D and A); lookup tables are premature |
| Enum serialization | Custom JsonConverter | `JsonStringEnumConverter` (in-box) | Already used for `HotCorner`; pattern is established |

**Key insight:** This phase is additive C# with no new external dependencies. Every pattern needed already exists in the codebase — extend rather than invent.

---

## Common Pitfalls

### Pitfall 1: STJ Deserializes Dictionary Keys as Strings, Not Enums by Default

**What goes wrong:** `Dictionary<HotCorner, CornerAction>` serialized with `JsonStringEnumConverter` added to `JsonSerializerOptions.Converters` works for values, but dictionary keys serialized as JSON object property names need a `JsonStringEnumConverter` applied at the type level OR `JsonSerializerOptions` must include the converter globally. Without it, STJ tries to use the integer ordinal as the key name and fails to round-trip.

**Why it happens:** JSON object keys are always strings. STJ's built-in enum key support requires the converter to be present — and the project's existing `JsonOptions` adds `new JsonStringEnumConverter()` to `Converters`, which handles both key and value positions since .NET 7+.

**How to avoid:** Verify round-trip in a quick test: serialize `{"TopLeft": "TaskView", ...}` and re-read it. The existing `JsonOptions` in `ConfigManager` should work as-is.

**Warning signs:** `JsonException: The JSON value could not be converted to WindowsHotSpot.Config.HotCorner` on Load().

### Pitfall 2: Migration Save() Fires SettingsChanged Prematurely

**What goes wrong:** `MigrateV1()` calls `Save()` which calls `SettingsChanged?.Invoke()`. At the point `Load()` is called during startup, `CornerDetector` hasn't been created yet, so there are no subscribers — but the event fires anyway. This is safe currently but could cause issues if listeners are attached before construction completes in future refactors.

**How to avoid:** Either call a `SaveRaw()` overload (no event) during migration, or document the call order clearly: `ConfigManager.Load()` must be called before any `SettingsChanged` listeners are attached.

**Warning signs:** NullReferenceException or premature state transitions if construction order changes.

### Pitfall 3: Partial CornerActions Dictionary After Corrupt/Partial JSON

**What goes wrong:** A hand-edited or corrupt settings.json might have only some corner keys. `CornerDetector` calls `CornerActions[_activeCorner]` which throws `KeyNotFoundException` if that corner isn't present.

**How to avoid:** After deserialization in `Load()`, fill missing keys:
```csharp
foreach (HotCorner c in Enum.GetValues<HotCorner>())
    Settings.CornerActions.TryAdd(c, CornerAction.Disabled);
```

**Warning signs:** `KeyNotFoundException` in `ActionDispatcher.Dispatch` or `CornerDetector.OnDwellComplete`.

### Pitfall 4: cbSize Must Use Marshal.SizeOf\<INPUT\>() — Already Solved, Don't Break It

**What goes wrong:** Copying `SendWinKey` into `ActionDispatcher` without the `Marshal.SizeOf<NativeMethods.INPUT>()` cbSize call (e.g., hardcoding 40 or using `sizeof`) causes `SendInput` to silently return 0.

**How to avoid:** Always pass `Marshal.SizeOf<NativeMethods.INPUT>()` as the third argument. This is documented in `MEMORY.md` and in `NativeMethods.cs` comments.

**Warning signs:** `SendInput` returns 0; action appears to do nothing.

### Pitfall 5: LegacyCorner Property Name Must Match JSON Field Name

**What goes wrong:** The v1 JSON has `"Corner": "TopLeft"`. If the C# property is named `LegacyCorner` without `[JsonPropertyName("Corner")]`, STJ will not map it and migration will silently never trigger.

**How to avoid:** Add `[JsonPropertyName("Corner")]` to the `LegacyCorner` property, or name the property exactly `Corner` (with documentation noting it's the v1 field).

**Warning signs:** Users who upgrade from v1.x lose their configured corner (resets to all-Disabled).

---

## Code Examples

Verified patterns from the project codebase:

### VK Constants for New Actions (add to NativeMethods.cs)
```csharp
// Source: Win32 Virtual-Key Codes — https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes
public const ushort VK_D = 0x44;   // Win+D → Show Desktop
public const ushort VK_A = 0x41;   // Win+A → Action Center
```

### CornerAction Enum (add to Config/AppSettings.cs)
```csharp
// Source: project pattern — mirrors existing HotCorner enum style
[JsonConverter(typeof(JsonStringEnumConverter))]
internal enum CornerAction { Disabled, TaskView, ShowDesktop, ActionCenter }
```

### Ensuring Complete Dictionary After Deserialize (in ConfigManager.Load)
```csharp
// Source: .NET 10 Dictionary.TryAdd
foreach (HotCorner c in Enum.GetValues<HotCorner>())
    Settings.CornerActions.TryAdd(c, CornerAction.Disabled);
```

### LegacyCorner Property (in AppSettings.cs)
```csharp
// Captures v1 "Corner" field; written as nothing when null (migration complete).
[JsonPropertyName("Corner")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public HotCorner? LegacyCorner { get; set; }
```

---

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Single active corner stored as enum | Per-corner dictionary `Dictionary<HotCorner, CornerAction>` | Phase 2 | Each corner independently configurable |
| `ActionTrigger.SendTaskView()` called directly from CornerDetector | `ActionDispatcher.Dispatch(CornerAction)` called from CornerDetector | Phase 2 | Dispatch is now action-polymorphic |
| AppSettings has `Corner` property | AppSettings has `CornerActions` dict; `Corner` becomes migration-only `LegacyCorner` | Phase 2 | v1 files migrate on first load |

**Deprecated/outdated after Phase 2:**
- `AppSettings.Corner`: replaced by `AppSettings.CornerActions[corner]`; old property kept as `LegacyCorner` for migration only
- Direct `ActionTrigger.SendTaskView()` call in `CornerDetector.OnDwellComplete`: replaced by `ActionDispatcher.Dispatch()`

---

## Open Questions

1. **Should `MigrateV1` call full `Save()` (fires SettingsChanged) or a silent file-write?**
   - What we know: at the time `Load()` is called in `HotSpotApplicationContext`, `SettingsChanged` has no subscribers yet (CornerDetector constructed after Load)
   - What's unclear: whether future refactors could change construction order
   - Recommendation: Add a private `SaveFile()` that writes JSON without firing the event; use it for migration; use public `Save()` for user-initiated saves. Documents the intent clearly.

2. **Where does CornerDetector get the action to dispatch?**
   - What we know: currently it takes `HotCorner corner` as a constructor argument and calls `ActionTrigger.SendTaskView()` directly
   - What's unclear: whether to pass the full `AppSettings` reference or just the `CornerAction` value
   - Recommendation: Pass a `Func<CornerAction>` delegate or a reference to `AppSettings` — the latter is simpler and consistent with how `HotSpotApplicationContext` already wires `SettingsChanged`. A direct `AppSettings` reference (or `ConfigManager` reference) is the idiomatic approach for this codebase.

3. **Is Win+A the correct shortcut for Action Center on Windows 11?**
   - What we know: Win+A opens Quick Settings (formerly Action Center) on Windows 11. The requirements say "Action Center (Win+A)."
   - What's unclear: whether the app should target Win 10 Action Center (Win+A was Cortana on Win 10) vs Win 11 only
   - Recommendation: The project targets Windows (installer, manifest) and the requirement says Win+A. Implement Win+A as specified; document the Win 11 assumption in a code comment.

---

## Sources

### Primary (HIGH confidence)
- Project codebase (`NativeMethods.cs`, `AppSettings.cs`, `ConfigManager.cs`, `ActionTrigger.cs`, `CornerDetector.cs`, `HotSpotApplicationContext.cs`) — direct read, authoritative for current state
- `MEMORY.md` — project-specific architecture decisions and known pitfalls
- Microsoft Learn: Virtual-Key Codes — https://learn.microsoft.com/en-us/windows/win32/inputdev/virtual-key-codes (VK_D = 0x44, VK_A = 0x41)
- Microsoft Learn: System.Text.Json unknown properties — default behavior is to skip unknown JSON properties (verified in .NET docs)

### Secondary (MEDIUM confidence)
- `REQUIREMENTS.md` and `ROADMAP.md` — authoritative for phase scope; phase 2 requirements confirmed as CONF-01, CRNA-01..04, CRNA-06
- .NET 10 `Dictionary<TKey,TValue>.TryAdd` — in-box, stable API

### Tertiary (LOW confidence)
- Win+A = Action Center on Windows 11: widely reported but not verified against official Windows 11 shortcut reference; aligns with requirements spec

---

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH — no new dependencies; all patterns already in codebase
- Architecture: HIGH — migration pattern is straightforward STJ nullable-property approach; ActionDispatcher is a thin wrapper over proven SendInput code
- Pitfalls: HIGH — cbSize issue is documented and understood; STJ enum-key behavior is the main new risk, mitigated by verifying existing `JsonOptions`

**Research date:** 2026-03-17
**Valid until:** 2026-06-17 (stable domain — .NET 10, System.Text.Json, Win32 VK codes all stable)
