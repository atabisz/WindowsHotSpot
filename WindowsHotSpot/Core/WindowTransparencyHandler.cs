// WindowTransparencyHandler: adjusts window transparency when Ctrl+Alt+Shift+Scroll is detected.
// Installs its own WH_KEYBOARD_LL hook to track LCtrl/LAlt/LShift state.
// CRITICAL: _kbCallback stored as class-level field to prevent GC collection.
// CRITICAL: KeyboardCallback must return in <300ms or Windows silently removes the hook.

using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using WindowsHotSpot.Config;
using WindowsHotSpot.Native;

namespace WindowsHotSpot.Core;

internal sealed class WindowTransparencyHandler : IDisposable
{
    // Keyboard hook
    private IntPtr _kbHookId = IntPtr.Zero;
    private readonly NativeMethods.LowLevelKeyboardProc _kbCallback;  // GC-pinned

    // Modifier state
    private bool _lCtrlDown;
    private bool _lAltDown;
    private bool _lShiftDown;

    private readonly AppSettings _settings;

    public WindowTransparencyHandler(AppSettings settings)
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

    /// <summary>Registered as HookManager.WheelSuppressionPredicate (combined in Phase 12).
    /// Suppresses WM_MOUSEWHEEL when LCtrl+LAlt+LShift is held.</summary>
    public bool ShouldSuppressWheel(int msg) => _lCtrlDown && _lAltDown && _lShiftDown;

    /// <summary>Called by HookManager.MouseWheeled. Adjusts window transparency when Ctrl+Alt+Shift is held.</summary>
    public void OnMouseWheeled(int delta, Point cursorPos)
    {
        // Self-heal modifier state (SAS can swallow key-up events)
        _lCtrlDown  = _lCtrlDown  && IsPhysicallyDown((int)NativeMethods.VK_LCONTROL);
        _lAltDown   = _lAltDown   && IsPhysicallyDown((int)NativeMethods.VK_LMENU);
        _lShiftDown = _lShiftDown && IsPhysicallyDown((int)NativeMethods.VK_LSHIFT);

        if (!_lCtrlDown || !_lAltDown || !_lShiftDown) return;   // TRNSP-01 gate

        var pt = new NativeMethods.POINT { X = cursorPos.X, Y = cursorPos.Y };

        // Find root top-level window under cursor
        var rawHwnd = NativeMethods.WindowFromPoint(pt);
        if (rawHwnd == IntPtr.Zero) return;
        var rootHwnd = NativeMethods.GetAncestor(rawHwnd, NativeMethods.GA_ROOT);
        if (rootHwnd == IntPtr.Zero) return;

        // GUARD-02: skip maximized windows
        var wp = new NativeMethods.WINDOWPLACEMENT();
        wp.length = (uint)Marshal.SizeOf<NativeMethods.WINDOWPLACEMENT>();
        if (!NativeMethods.GetWindowPlacement(rootHwnd, ref wp)) return;
        if (wp.showCmd == NativeMethods.SW_SHOWMAXIMIZED) return;

        // GUARD-03: skip elevated windows
        if (IsElevatedProcess(rootHwnd)) return;

        // Read current extended style
        int exStyle = NativeMethods.GetWindowLong(rootHwnd, NativeMethods.GWL_EXSTYLE);

        // Read existing alpha and flags to preserve LWA_COLORKEY on color-key windows — TRNSP-03
        // GetLayeredWindowAttributes only succeeds if WS_EX_LAYERED was set via SetLayeredWindowAttributes
        // (not UpdateLayeredWindow). On failure use defaults: alpha=255 (opaque), no flags.
        byte existingAlpha = 255;
        uint existingFlags = 0;
        if ((exStyle & (int)NativeMethods.WS_EX_LAYERED) != 0)
            NativeMethods.GetLayeredWindowAttributes(
                rootHwnd, out _, out existingAlpha, out existingFlags);

        // Compute new alpha — TRNSP-02 (scroll up = more opaque), TRNSP-05 (clamp 25-255)
        int step      = delta * _settings.TransparencyStep / 120;
        byte newAlpha = (byte)Math.Clamp(existingAlpha + step, 25, 255);

        // Ensure WS_EX_LAYERED is set before calling SetLayeredWindowAttributes
        if ((exStyle & (int)NativeMethods.WS_EX_LAYERED) == 0)
            NativeMethods.SetWindowLongPtr(
                rootHwnd,
                NativeMethods.GWL_EXSTYLE,
                new IntPtr(exStyle | (int)NativeMethods.WS_EX_LAYERED));

        // Apply alpha; OR in LWA_ALPHA so it is always active alongside any existing color-key flag — TRNSP-03
        NativeMethods.SetLayeredWindowAttributes(
            rootHwnd, 0, newAlpha, existingFlags | NativeMethods.LWA_ALPHA);
    }

    // Returns true if the key (VK_* constant) is physically held down right now.
    // Bit 15 of GetKeyState return value is the key-down flag.
    private static bool IsPhysicallyDown(int vk) => (NativeMethods.GetKeyState(vk) & 0x8000) != 0;

    // Returns true if the window's owning process is running elevated (admin).
    // Transparency adjustment is blocked against elevated windows — UIPI prevents SetLayeredWindowAttributes cross-elevation.
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
                    case NativeMethods.VK_LSHIFT:
                        if (isKeyDown) _lShiftDown = true;   // no !isInjected guard — AltGr does not synthesize LShift
                        if (isKeyUp)   _lShiftDown = false;
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
        _lCtrlDown  = false;
        _lAltDown   = false;
        _lShiftDown = false;
    }
}
