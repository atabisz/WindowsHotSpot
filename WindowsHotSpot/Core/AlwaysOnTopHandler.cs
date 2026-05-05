// AlwaysOnTopHandler: pins/unpins the window under the cursor when Ctrl+Alt double-click is detected.
// Installs its own WH_KEYBOARD_LL hook to track LCtrl/LAlt state.
// CRITICAL: _kbCallback stored as class-level field to prevent GC collection.
// CRITICAL: KeyboardCallback must return in <300ms or Windows silently removes the hook.

using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WindowsHotSpot.Config;
using WindowsHotSpot.Native;

namespace WindowsHotSpot.Core;

internal sealed class AlwaysOnTopHandler : IDisposable
{
    // Keyboard hook
    private IntPtr _kbHookId = IntPtr.Zero;
    private readonly NativeMethods.LowLevelKeyboardProc _kbCallback;  // GC-pinned

    // Modifier state
    private bool _lCtrlDown;
    private bool _lAltDown;

    // Double-click tracking
    private uint _lastDownTime;
    private Point _lastDownPt;

    private readonly AppSettings _settings;
    private readonly NotifyIcon  _trayIcon;

    public AlwaysOnTopHandler(AppSettings settings, NotifyIcon trayIcon)
    {
        _settings   = settings;
        _trayIcon   = trayIcon;
        _kbCallback = KeyboardCallback;  // field reference prevents GC collection
    }

    public void Install()
    {
        if (_kbHookId != IntPtr.Zero)
            throw new InvalidOperationException("Hook is already installed. Call Dispose() before re-installing.");

        using var process = System.Diagnostics.Process.GetCurrentProcess();
        using var module  = process.MainModule!;
        _kbHookId = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _kbCallback,
            NativeMethods.GetModuleHandle(module.ModuleName!),
            0);

        if (_kbHookId == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to install keyboard hook.");
    }

    /// <summary>Called by HookManager.MouseButtonChanged. Toggles always-on-top on confirmed Ctrl+Alt double-click.</summary>
    public void OnMouseButtonChanged(bool isDown)
    {
        if (!isDown) return;   // only track button-down events

        // Self-heal: SAS can swallow key-up events (AOT-04)
        _lCtrlDown = _lCtrlDown && IsPhysicallyDown((int)NativeMethods.VK_LCONTROL);
        _lAltDown  = _lAltDown  && IsPhysicallyDown((int)NativeMethods.VK_LMENU);

        if (!_lCtrlDown || !_lAltDown)
        {
            _lastDownTime = 0;
            _lastDownPt   = default;
            return;
        }

        var cursorPos  = System.Windows.Forms.Cursor.Position;
        uint now       = (uint)Environment.TickCount;
        uint timeDelta = now - _lastDownTime;

        bool withinTime = _lastDownTime != 0
                       && timeDelta <= NativeMethods.GetDoubleClickTime();
        bool withinDist = Math.Abs(cursorPos.X - _lastDownPt.X)
                            <= NativeMethods.GetSystemMetrics(NativeMethods.SM_CXDOUBLECLK)
                       && Math.Abs(cursorPos.Y - _lastDownPt.Y)
                            <= NativeMethods.GetSystemMetrics(NativeMethods.SM_CYDOUBLECLK);

        if (withinTime && withinDist)
        {
            // Double-click confirmed — reset tracking FIRST so a third click starts fresh
            _lastDownTime = 0;
            _lastDownPt   = default;

            ToggleAlwaysOnTop(cursorPos);
        }
        else
        {
            _lastDownTime = now;
            _lastDownPt   = cursorPos;
        }
    }

    private void ToggleAlwaysOnTop(Point cursorPos)
    {
        var pt      = new NativeMethods.POINT { X = cursorPos.X, Y = cursorPos.Y };
        var rawHwnd = NativeMethods.WindowFromPoint(pt);
        if (rawHwnd == IntPtr.Zero) return;

        var rootHwnd = NativeMethods.GetAncestor(rawHwnd, NativeMethods.GA_ROOT);
        if (rootHwnd == IntPtr.Zero) return;

        // AOT-03: skip elevated windows — UIPI prevents SetWindowPos cross-elevation
        if (IsElevatedProcess(rootHwnd)) return;

        int exStyle    = NativeMethods.GetWindowLong(rootHwnd, NativeMethods.GWL_EXSTYLE);
        bool isTopmost = (exStyle & (int)NativeMethods.WS_EX_TOPMOST) != 0;

        const uint SWP_FLAGS =
            NativeMethods.SWP_NOMOVE |
            NativeMethods.SWP_NOSIZE |
            NativeMethods.SWP_NOACTIVATE;

        NativeMethods.SetWindowPos(
            rootHwnd,
            isTopmost ? NativeMethods.HWND_NOTOPMOST : NativeMethods.HWND_TOPMOST,
            0, 0, 0, 0,
            SWP_FLAGS);

        // AOT-02: balloon feedback
        _trayIcon.ShowBalloonTip(
            2000,
            "WindowsHotSpot",
            isTopmost ? "Unpinned" : "Pinned on top",
            ToolTipIcon.Info);
    }

    // Returns true if the key (VK_* constant) is physically held down right now.
    // Bit 15 of GetKeyState return value is the key-down flag.
    private static bool IsPhysicallyDown(int vk) => (NativeMethods.GetKeyState(vk) & 0x8000) != 0;

    // Returns true if the window's owning process is running elevated (admin).
    // AOT toggle is blocked against elevated windows — UIPI prevents SetWindowPos cross-elevation.
    private static bool IsElevatedProcess(IntPtr hwnd)
    {
        NativeMethods.GetWindowThreadProcessId(hwnd, out uint pid);
        if (pid == 0) return false;

        var hProcess = NativeMethods.OpenProcess(NativeMethods.PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
        if (hProcess == IntPtr.Zero) return true;  // can't open → assume elevated, bail out safely

        try
        {
            if (!NativeMethods.OpenProcessToken(hProcess, NativeMethods.TOKEN_QUERY, out var hToken))
                return true;

            try
            {
                bool ok = NativeMethods.GetTokenInformation(hToken, NativeMethods.TokenElevation,
                    out uint elevation, sizeof(uint), out _);
                return ok && elevation != 0;
            }
            finally { NativeMethods.CloseHandle(hToken); }
        }
        finally { NativeMethods.CloseHandle(hProcess); }
    }

    private IntPtr KeyboardCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            try
            {
                var kb = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
                bool isKeyDown  = (int)wParam == NativeMethods.WM_KEYDOWN
                               || (int)wParam == NativeMethods.WM_SYSKEYDOWN;
                bool isKeyUp    = (int)wParam == NativeMethods.WM_KEYUP
                               || (int)wParam == NativeMethods.WM_SYSKEYUP;
                bool isInjected = (kb.flags & NativeMethods.LLKHF_INJECTED) != 0;

                switch (kb.vkCode)
                {
                    case NativeMethods.VK_LCONTROL:
                        if (isKeyDown && !isInjected) _lCtrlDown = true;   // GUARD-01: skip injected (AltGr fake LCtrl)
                        if (isKeyUp)                  _lCtrlDown = false;
                        break;
                    case NativeMethods.VK_LMENU:
                        if (isKeyDown) _lAltDown = true;
                        if (isKeyUp)   _lAltDown = false;
                        break;
                    // VK_RMENU (AltGr, 0xA5) deliberately not tracked — GUARD-01
                }
            }
            catch
            {
                // Never let managed exceptions escape into the Win32 hook chain. Fail safe.
            }
        }
        return NativeMethods.CallNextHookEx(_kbHookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_kbHookId != IntPtr.Zero)
        {
            bool ok = NativeMethods.UnhookWindowsHookEx(_kbHookId);
            System.Diagnostics.Debug.Assert(ok,
                $"UnhookWindowsHookEx failed: {Marshal.GetLastWin32Error()}");
            _kbHookId = IntPtr.Zero;
        }
        _lCtrlDown = false;
        _lAltDown  = false;
    }
}
