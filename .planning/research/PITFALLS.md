# Domain Pitfalls

**Domain:** Windows system tray app with global low-level mouse hook (C# WinForms)
**Researched:** 2026-03-11

## Critical Pitfalls

Mistakes that cause the app to silently break, crash, or require rewrites.

### Pitfall 1: Hook Timeout Causes Silent Removal (Windows 7+)

**What goes wrong:** The low-level mouse hook callback (`LowLevelMouseProc`) takes too long to return. Windows silently removes the hook from the chain. The app keeps running but stops receiving mouse events entirely -- no error, no notification, no way to detect the removal programmatically.

**Why it happens:** Microsoft documents that on Windows 7 and later, if the hook procedure does not return within the `LowLevelHooksTimeout` registry value (under `HKEY_CURRENT_USER\Control Panel\Desktop`), the hook is silently removed. On Windows 10 1709+, the maximum allowed timeout is 1000ms (1 second), even if the registry is set higher.

Common causes of slow callbacks:
- Doing any work beyond trivial coordinate checks (file I/O, network, logging, UI updates)
- Garbage collection pauses in .NET blocking the hook thread
- The hook thread's message pump being blocked by a modal dialog or long operation
- Calling `SendMessage` to another window from within the callback (synchronous cross-thread call)

**Consequences:** The hot corner silently stops working. The user has no indication that anything is wrong. The system tray icon remains, but the app is dead functionally.

**Prevention:**
- Do absolute minimum work in the hook callback: read coordinates, compare against corner bounds, set a flag or post a message. Nothing else.
- Never allocate memory, do I/O, or call `SendMessage` from the callback.
- Use `PostMessage` or set a volatile flag that a separate timer/thread checks.
- Run the hook on a dedicated thread with its own message loop, isolated from the UI thread. This is explicitly recommended by Microsoft in the `LowLevelMouseProc` documentation.
- Implement a periodic health check: a timer that verifies the hook is still alive by checking `SetWindowsHookEx` return value or reinstalling the hook on a schedule (e.g., every 30 seconds).

**Detection:**
- Hot corner stops triggering but the app is still in the tray.
- No error messages, no exceptions -- pure silent failure.
- Test by adding artificial delay in the callback during development; confirm the hook dies.

**Confidence:** HIGH -- directly from Microsoft official documentation (`LowLevelMouseProc` remarks section, updated 2026-02-24).

**Phase relevance:** Must be addressed in Phase 1 (core hook implementation). This is the single most important architectural decision.

---

### Pitfall 2: Garbage Collector Collects the Hook Callback Delegate

**What goes wrong:** The .NET garbage collector relocates or collects the delegate passed to `SetWindowsHookEx`, causing an `ExecutionEngineException` crash or silent hook death.

**Why it happens:** `SetWindowsHookEx` receives a native function pointer. If the managed delegate is only referenced by the P/Invoke call (e.g., passed as a lambda or local variable), the GC sees no managed references and collects it. The native side then calls into freed/moved memory.

**Consequences:** Application crash (`ExecutionEngineException`, `AccessViolationException`) or silent hook removal. Intermittent and hard to reproduce -- may work fine for hours then fail during a GC cycle under memory pressure.

**Prevention:**
- Store the delegate in a **static field** or a long-lived instance field. Microsoft's `SetWindowsHookEx` documentation explicitly states: "you must ensure the callback is not moved around by the garbage collector... One way to do this is by making the callback a static method of your class."
- Use `GCHandle.Alloc` with `GCHandleType.Normal` as a belt-and-suspenders approach, though a static field reference is sufficient and simpler.

**Detection:**
- Random crashes that don't reproduce reliably.
- Crashes that correlate with GC pressure (opening settings dialogs, loading resources).
- Running with `GC.Collect()` calls in debug mode can force the issue to surface.

**Confidence:** HIGH -- directly from Microsoft `SetWindowsHookEx` documentation remarks.

**Phase relevance:** Phase 1. Must be correct from day one.

---

### Pitfall 3: SendInput Blocked by UIPI When Foreground App Is Elevated

**What goes wrong:** The app sends Win+Tab via `SendInput` to trigger Task View, but the input is silently dropped because the foreground window belongs to an elevated (admin) process. `SendInput` returns 0 but `GetLastError` does NOT indicate the UIPI failure.

**Why it happens:** User Interface Privilege Isolation (UIPI) prevents a medium-integrity process from sending input to a high-integrity (elevated) process. Microsoft documents: "This function fails when it is blocked by UIPI. Note that neither GetLastError nor the return value will indicate the failure was caused by UIPI blocking."

Since the PROJECT.md explicitly states "No admin required" and the app runs without elevation, this is an inherent limitation.

**Consequences:** Hot corner works most of the time but silently fails when the user has an elevated application in the foreground (e.g., Task Manager run as admin, Registry Editor, an installer, UAC prompt). This will feel like a bug to users.

**Prevention:**
- Accept this as a known limitation and document it. This is the same limitation every non-elevated automation tool has (AutoHotkey, PowerToys, etc.).
- Do NOT attempt to run the app elevated as a workaround -- this would break the "no admin required" constraint and introduce other problems (registry writes to wrong hive, UAC prompts on startup).
- Consider: Win+Tab is sent to the shell (Explorer.exe), which runs at medium integrity. The input may actually work even when an elevated app is in the foreground, because the shell processes the Win key combination. **This needs empirical testing.**
- If testing shows UIPI is a real problem, the only clean workaround is a `UIAccess=true` manifest entry with the binary signed and installed in a trusted location (Program Files). This is how Windows on-screen keyboard and Magnifier work.

**Detection:**
- Hot corner fails only when elevated apps are focused.
- Test by running Task Manager as admin, focusing it, then triggering the hot corner.

**Confidence:** MEDIUM -- the UIPI constraint is documented (HIGH), but whether Win+Tab specifically is affected when targeting the shell is uncertain and needs testing.

**Phase relevance:** Phase 1 (testing), Phase 3 (installer -- if UIAccess manifest is needed, the binary must be signed and in Program Files).

---

### Pitfall 4: Multi-Monitor DPI Scaling Breaks Corner Detection

**What goes wrong:** The app uses `Screen.Bounds` or `Cursor.Position` to detect screen corners, but these return virtualized coordinates that don't match actual pixel positions on high-DPI or mixed-DPI multi-monitor setups.

**Why it happens:** By default, .NET WinForms apps run as "System DPI aware." On a multi-monitor setup where monitors have different DPI settings, Windows virtualizes coordinates for non-primary monitors. A monitor at 150% scaling will report incorrect bounds to a System-DPI-aware app. The "corner" coordinates the app calculates may be off by hundreds of pixels.

Per Microsoft's high-DPI documentation: "if a DPI-unaware thread queries the screen size while running on a high-DPI display, Windows will virtualize the answer." System DPI aware apps get virtualized values for any display at a different DPI than the primary.

**Consequences:**
- Hot corner triggers in the wrong physical location on secondary monitors.
- Hot corner never triggers on some monitors because the calculated corner position doesn't match where the mouse cursor actually reaches.
- Works fine on single-monitor setups, breaks in the common laptop+external-monitor scenario.

**Prevention:**
- Declare the app as **Per-Monitor V2 DPI aware** via application manifest. This gives the app unvirtualized (raw physical pixel) coordinates from all monitors.
- In the app manifest (app.manifest), add:
  ```xml
  <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2016/WindowsSettings">PerMonitorV2</dpiAwareness>
  ```
- Use `Screen.AllScreens` with per-monitor awareness to get correct bounds.
- Alternatively, use the Win32 API `EnumDisplayMonitors` + `GetMonitorInfo` for ground-truth monitor geometry when per-monitor aware.
- The low-level mouse hook's `MSLLHOOKSTRUCT.pt` provides coordinates in the **physical (unvirtualized) coordinate space** regardless of the calling thread's DPI awareness. This is important -- the hook gives you real coordinates, but `Screen.Bounds` may lie if DPI awareness isn't set correctly.

**Detection:**
- Test on a dual-monitor setup with different DPI settings (e.g., laptop at 150%, external at 100%).
- Hot corner works on the primary monitor but fails on secondary monitors.

**Confidence:** HIGH -- Microsoft's high-DPI documentation is explicit about coordinate virtualization and per-monitor awareness.

**Phase relevance:** Phase 1 (architecture), Phase 2 (multi-monitor support). Must be decided early because it affects how all coordinate math works.

---

### Pitfall 5: Hook Not Unregistered on Crash/Exit

**What goes wrong:** The app exits (normally or via crash) without calling `UnhookWindowsHookEx`. The hook remains registered in the system hook chain, consuming resources and potentially causing input lag for all applications.

**Why it happens:** Common scenarios:
- Unhandled exception crashes the process before cleanup runs.
- `Environment.Exit()` or `Application.Exit()` called without cleanup.
- User kills the process via Task Manager.
- Finalizer/destructor not guaranteed to run.

**Consequences:** Orphaned hooks degrade system input responsiveness. While Windows eventually cleans up hooks from dead processes, the behavior during the interim can cause noticeable mouse lag for all applications.

**Prevention:**
- Call `UnhookWindowsHookEx` in the `ApplicationExit` event handler.
- Also call it in a `try/finally` block around the message loop.
- Register an `AppDomain.CurrentDomain.UnhandledException` handler that unhooks.
- Implement `IDisposable` on the hook wrapper class and use `using` statements.
- Accept that `taskkill /f` and hard crashes will orphan the hook -- Windows will clean it up when it detects the owning thread is gone, but document this behavior.

**Detection:**
- After force-killing the app, check if mouse feels sluggish briefly.
- Verify cleanup by checking the hook handle is invalidated on normal exit.

**Confidence:** HIGH -- standard Win32 resource management, documented in `SetWindowsHookEx` remarks.

**Phase relevance:** Phase 1 (resource cleanup must be designed in from the start).

---

## Moderate Pitfalls

### Pitfall 6: Message Pump Starvation Kills the Hook

**What goes wrong:** The low-level hook requires a message pump on the thread that installed it. If the message pump blocks (modal dialog, long synchronous operation, `Thread.Sleep`), the hook stops processing events and may be removed by Windows.

**Why it happens:** Microsoft documents: "the thread that installed the hook must have a message loop" and the hook is "called in the context of the thread that installed it. The call is made by sending a message to the thread that installed the hook." If the message loop isn't pumping, the hook messages queue up and eventually time out.

**Prevention:**
- Run the hook on a dedicated thread with a clean `Application.Run()` or `while(GetMessage(...))` message loop. Never share this thread with UI work.
- Never show modal dialogs on the hook thread.
- The WinForms UI thread can host the hook IF the app never blocks it -- but this is fragile. A dedicated thread is safer.

**Confidence:** HIGH -- directly from Microsoft documentation.

**Phase relevance:** Phase 1 (thread architecture).

---

### Pitfall 7: Accidental Double-Trigger of Task View

**What goes wrong:** The hot corner triggers Task View, but the mouse is still in the corner, so it triggers again immediately when Task View closes or even while Task View is open.

**Why it happens:** The dwell timer fires, sends Win+Tab, but the mouse hasn't moved out of the corner zone. Without a cooldown or state machine, the next dwell period starts immediately and fires again.

**Prevention:**
- Implement a state machine with states: `Idle -> Dwelling -> Triggered -> Cooldown`.
- After triggering, enter a cooldown state that requires the mouse to **leave the corner zone** before re-arming.
- Do not re-arm on a timer alone -- require physical mouse movement out of the zone.
- Consider: Task View toggle behavior means a second Win+Tab closes it. A double-trigger would open then immediately close Task View, appearing as a flicker or no-op to the user.

**Detection:**
- Task View flashes briefly then closes.
- Task View doesn't appear at all (double-trigger opens and immediately closes it).

**Confidence:** HIGH -- standard hot corner implementation concern, well-documented in macOS hot corner implementations.

**Phase relevance:** Phase 1 (core behavior logic).

---

### Pitfall 8: Hook Callback Re-Entrancy

**What goes wrong:** The hook callback is invoked re-entrantly (a second call arrives while the first is still processing), leading to race conditions, stack overflow, or corrupted state.

**Why it happens:** If the hook callback calls any function that pumps messages (even indirectly -- e.g., `SendMessage`, COM calls, certain .NET APIs), Windows may deliver another hook notification on the same thread before the first callback returns.

**Prevention:**
- Keep the callback trivially simple -- read coordinates, do a bounds check, set a flag. No message pumping, no COM, no UI calls.
- If re-entrancy is a concern, use a simple boolean guard (`if (inCallback) return CallNextHookEx(...)`) but this should be unnecessary if the callback is truly minimal.
- Never call `SendMessage` from the callback; use `PostMessage` instead.

**Confidence:** MEDIUM -- re-entrancy is a documented concern for hook callbacks in general, though with a minimal callback it is unlikely to occur.

**Phase relevance:** Phase 1 (callback implementation).

---

### Pitfall 9: WinForms Settings Dialog Blurry on High DPI

**What goes wrong:** The settings dialog appears blurry or has misaligned controls on high-DPI displays.

**Why it happens:** WinForms has "limited" per-monitor DPI scaling support (per Microsoft's own documentation table). Controls may not resize correctly when the dialog is moved between monitors with different DPI. Font sizes, padding, and control positions set in the designer assume 96 DPI.

**Prevention:**
- Set `AutoScaleMode = AutoScaleMode.Dpi` on all forms.
- In the `.csproj`, ensure high-DPI support is enabled:
  ```xml
  <ApplicationHighDpiMode>PerMonitorV2</ApplicationHighDpiMode>
  ```
- Use `Font` scaling rather than pixel-based sizing where possible.
- Test the dialog at 100%, 125%, 150%, and 200% scaling.
- Use `TableLayoutPanel` and `FlowLayoutPanel` for layouts that adapt to scaling rather than absolute positioning.
- Accept that WinForms high-DPI is "limited" -- keep the settings dialog simple (a few dropdowns, a slider, a checkbox) to minimize exposure to scaling issues.

**Confidence:** HIGH -- Microsoft's DPI documentation explicitly calls out WinForms as having "limited automatic per-monitor DPI scaling."

**Phase relevance:** Phase 2 (settings dialog).

---

### Pitfall 10: "Start with Windows" Registry Key Points to Wrong Path

**What goes wrong:** The app registers itself in `HKCU\Software\Microsoft\Windows\CurrentVersion\Run` with a path that becomes invalid after the user moves, updates, or reinstalls the app.

**Why it happens:** The app writes `Application.ExecutablePath` to the registry at the time the checkbox is toggled. If the user later moves the app folder, uninstalls and reinstalls to a different location, or runs from a development build path vs installed path, the registry entry points to a nonexistent executable. Windows silently ignores invalid Run entries.

**Prevention:**
- Always update the registry entry on every app startup (not just when the checkbox is toggled). If "Start with Windows" is enabled, rewrite the current path.
- Use the installed path from the shortcut/installer context, not `Application.ExecutablePath` from a debug session.
- On startup, verify the registry path matches the current executable path and correct it if needed.

**Confidence:** HIGH -- standard Windows auto-start pattern, well-known issue.

**Phase relevance:** Phase 2 (settings/startup).

---

## Minor Pitfalls

### Pitfall 11: Coordinate Off-by-One at Screen Edges

**What goes wrong:** The mouse cursor is clamped to the screen edge by Windows, and the exact corner pixel may or may not be reachable depending on the monitor arrangement and cursor clipping.

**Prevention:**
- Use a zone (e.g., 5x5 pixels from the corner) rather than checking for the exact corner pixel.
- The zone size should be configurable (PROJECT.md already specifies this).
- Test with different monitor arrangements (stacked, side-by-side, offset).

**Confidence:** MEDIUM -- depends on driver behavior and monitor arrangement.

**Phase relevance:** Phase 1 (corner detection logic).

---

### Pitfall 12: .NET Runtime Not Bundled -- App Fails to Launch

**What goes wrong:** The user installs the app but doesn't have .NET 8 runtime installed. The app fails to start with a cryptic error or a "download .NET" dialog.

**Prevention:**
- Publish as **self-contained** (`dotnet publish -r win-x64 --self-contained`). This bundles the .NET runtime with the app, making it ~60-80MB but eliminating runtime dependency issues.
- Alternatively, publish as framework-dependent and include the .NET runtime prerequisite in the NSIS/WiX installer.
- Self-contained + trimmed (`PublishTrimmed=true`) can reduce size to ~20-30MB.
- **Recommended:** Use self-contained + trimmed + single-file (`PublishSingleFile=true`) for the cleanest distribution.

**Confidence:** HIGH -- standard .NET deployment concern.

**Phase relevance:** Phase 3 (installer/distribution).

---

### Pitfall 13: System Tray Icon Persists After Crash

**What goes wrong:** If the app crashes, the system tray icon remains as a ghost until the user hovers over it, at which point Windows removes it.

**Prevention:**
- This is a Windows limitation -- there is no reliable way to remove a tray icon from a dead process.
- Mitigate by making the app robust (unhandled exception handlers, etc.) so it rarely crashes.
- On startup, the app could check for orphaned instances and clean up, but the ghost icon issue is cosmetic and resolves on hover.

**Confidence:** HIGH -- known Windows shell behavior, not fixable by the app.

**Phase relevance:** Phase 1 (robustness), accepted limitation.

---

### Pitfall 14: Installer Needs UAC for Program Files

**What goes wrong:** The installer requires admin privileges to write to Program Files, but the user expects a "no admin" experience based on the app's own non-elevated behavior.

**Prevention:**
- The installer needing admin is standard and expected -- this is different from the app itself needing admin. Users expect installers to prompt for UAC.
- Alternatively, offer a per-user install to `%LOCALAPPDATA%\Programs\WindowsHotSpot` which does not require elevation.
- If using UIAccess manifest (for UIPI bypass), the binary MUST be in a trusted location (Program Files or Windows directory), so the elevated installer becomes mandatory.

**Confidence:** HIGH -- standard Windows installer behavior.

**Phase relevance:** Phase 3 (installer).

---

## Phase-Specific Warnings

| Phase Topic | Likely Pitfall | Mitigation |
|---|---|---|
| Phase 1: Hook implementation | Hook timeout / silent removal (Pitfall 1) | Dedicated thread, minimal callback, health-check timer |
| Phase 1: Hook implementation | GC collects delegate (Pitfall 2) | Static field for delegate reference |
| Phase 1: Hook implementation | Message pump starvation (Pitfall 6) | Dedicated message loop thread |
| Phase 1: Trigger logic | Double-trigger (Pitfall 7) | State machine with leave-zone-to-rearm |
| Phase 1: Trigger logic | Re-entrancy (Pitfall 8) | Trivial callback, PostMessage not SendMessage |
| Phase 1: Corner detection | DPI coordinate mismatch (Pitfall 4) | Per-Monitor V2 manifest, use raw hook coordinates |
| Phase 1: Corner detection | Off-by-one at edges (Pitfall 11) | Zone-based detection, not single-pixel |
| Phase 1: Cleanup | Hook not unregistered (Pitfall 5) | IDisposable, multiple cleanup paths |
| Phase 2: SendInput | UIPI blocks input to elevated apps (Pitfall 3) | Accept limitation or use UIAccess manifest |
| Phase 2: Settings dialog | Blurry on high DPI (Pitfall 9) | AutoScaleMode.Dpi, simple layout |
| Phase 2: Auto-start | Registry path stale (Pitfall 10) | Rewrite path on every startup |
| Phase 3: Distribution | Missing .NET runtime (Pitfall 12) | Self-contained publish |
| Phase 3: Distribution | Installer needs UAC (Pitfall 14) | Expected behavior; offer per-user install option |
| All phases | Ghost tray icon (Pitfall 13) | Robustness; accepted limitation |

## Sources

- Microsoft `SetWindowsHookEx` documentation: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowshookexw (HIGH confidence, updated 2025-07-01)
- Microsoft `LowLevelMouseProc` documentation: https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc (HIGH confidence, updated 2026-02-24)
- Microsoft `SendInput` documentation: https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput (HIGH confidence, updated 2025-07-01)
- Microsoft High DPI Desktop Application Development: https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows (HIGH confidence, updated 2025-07-16)
