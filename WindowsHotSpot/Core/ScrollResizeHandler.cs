// ScrollResizeHandler: resizes the window under the cursor when Ctrl+Alt+Scroll is detected.
// Installs its own WH_KEYBOARD_LL hook to track LCtrl/LAlt state.
// CRITICAL: _kbCallback stored as class-level field to prevent GC collection.
// CRITICAL: KeyboardCallback must return in <300ms or Windows silently removes the hook.

using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using WindowsHotSpot.Config;
using WindowsHotSpot.Native;

namespace WindowsHotSpot.Core;

internal sealed class ScrollResizeHandler : IDisposable
{
    // Keyboard hook
    private IntPtr _kbHookId = IntPtr.Zero;
    private readonly NativeMethods.LowLevelKeyboardProc _kbCallback;  // GC-pinned

    // Modifier state
    private bool _lCtrlDown;
    private bool _lAltDown;

    private readonly AppSettings _settings;

    public ScrollResizeHandler(AppSettings settings)
    {
        _settings = settings;
        _kbCallback = KeyboardCallback;  // field reference prevents GC collection
    }

    public void Install()
    {
        if (_kbHookId != IntPtr.Zero)
            throw new InvalidOperationException("Hook is already installed. Call Dispose() before re-installing.");

        using var process = System.Diagnostics.Process.GetCurrentProcess();
        using var module = process.MainModule!;
        _kbHookId = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_KEYBOARD_LL,
            _kbCallback,
            NativeMethods.GetModuleHandle(module.ModuleName!),
            0);

        if (_kbHookId == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to install keyboard hook.");
    }

    /// <summary>Called by HookManager.MouseWheeled. Resizes the window under the cursor when Ctrl+Alt is held.</summary>
    public void OnMouseWheeled(int delta, Point cursorPos)
    {
        // Self-heal modifier state (SAS can swallow key-up events)
        _lCtrlDown = _lCtrlDown && IsPhysicallyDown((int)NativeMethods.VK_LCONTROL);
        _lAltDown  = _lAltDown  && IsPhysicallyDown((int)NativeMethods.VK_LMENU);

        if (!_lCtrlDown || !_lAltDown) return;   // RESIZE-01 gate

        var pt = new NativeMethods.POINT { X = cursorPos.X, Y = cursorPos.Y };

        // Find root top-level window under cursor
        var rawHwnd = NativeMethods.WindowFromPoint(pt);
        if (rawHwnd == IntPtr.Zero) return;
        var rootHwnd = NativeMethods.GetAncestor(rawHwnd, NativeMethods.GA_ROOT);
        if (rootHwnd == IntPtr.Zero) return;

        // RESIZE-05: skip maximized windows
        var wp = new NativeMethods.WINDOWPLACEMENT();
        wp.length = (uint)Marshal.SizeOf<NativeMethods.WINDOWPLACEMENT>();
        if (!NativeMethods.GetWindowPlacement(rootHwnd, ref wp)) return;
        if (wp.showCmd == NativeMethods.SW_SHOWMAXIMIZED) return;

        // RESIZE-06: skip elevated windows
        if (IsElevatedProcess(rootHwnd)) return;

        // Current rect
        if (!NativeMethods.GetWindowRect(rootHwnd, out var rect)) return;

        int w = rect.Right  - rect.Left;
        int h = rect.Bottom - rect.Top;

        // Zero-size window guard: avoid division by zero on degenerate window rect (T-09-04)
        if (w == 0 || h == 0) return;

        // Step size: delta is signed multiples of WHEEL_DELTA (120) — RESIZE-03
        int step = delta * _settings.ScrollResizeStep / 120;

        // Clamp to Windows minimum tracking size — RESIZE-04
        int minW = NativeMethods.GetSystemMetrics(NativeMethods.SM_CXMINTRACK);
        int minH = NativeMethods.GetSystemMetrics(NativeMethods.SM_CYMINTRACK);
        int nw = Math.Max(w + step, minW);
        int nh = Math.Max(h + step, minH);

        // Cursor-anchored position math — RESIZE-02
        // Preserve cursor's fractional position within the window after resize.
        double fx = (double)(cursorPos.X - rect.Left) / w;
        double fy = (double)(cursorPos.Y - rect.Top)  / h;
        int newLeft = cursorPos.X - (int)(fx * nw);
        int newTop  = cursorPos.Y - (int)(fy * nh);

        const uint SWP_FLAGS =
            NativeMethods.SWP_NOZORDER |
            NativeMethods.SWP_NOACTIVATE |
            NativeMethods.SWP_ASYNCWINDOWPOS;  // NO SWP_NOSIZE — this is a resize

        NativeMethods.SetWindowPos(rootHwnd, IntPtr.Zero, newLeft, newTop, nw, nh, SWP_FLAGS);
    }

    // Returns true if the key (VK_* constant) is physically held down right now.
    // Bit 15 of GetKeyState return value is the key-down flag.
    private static bool IsPhysicallyDown(int vk) => (NativeMethods.GetKeyState(vk) & 0x8000) != 0;

    // Returns true if the window's owning process is running elevated (admin).
    // Resize is blocked against elevated windows — UIPI prevents SetWindowPos cross-elevation.
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
