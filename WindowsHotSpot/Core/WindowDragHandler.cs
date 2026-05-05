// WindowDragHandler: installs WH_KEYBOARD_LL hook to track LCtrl+LAlt state.
// CRITICAL: _kbCallback stored as class-level field to prevent GC collection.
// CRITICAL: KeyboardCallback must return in <300ms or Windows silently removes the hook.

using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using WindowsHotSpot.Config;
using WindowsHotSpot.Native;

namespace WindowsHotSpot.Core;

internal sealed class WindowDragHandler : IDisposable
{
    // Keyboard hook
    private IntPtr _kbHookId = IntPtr.Zero;
    private readonly NativeMethods.LowLevelKeyboardProc _kbCallback;  // GC-pinned

    // Modifier state
    private bool _lCtrlDown;
    private bool _lAltDown;

    // Drag state
    private bool _isDragging;
    private bool _suppressNextClick;  // D-04: true when modifier+click on non-draggable surface and WindowDragPassThrough=false
    private IntPtr _dragTarget;
    private NativeMethods.POINT _dragStartCursor;
    private NativeMethods.RECT  _windowOrigin;

    // System cursors — loaded once at class init; shared resources, never call DestroyCursor
    private static readonly IntPtr _sizeAllCursor =
        NativeMethods.LoadCursor(IntPtr.Zero, NativeMethods.IDC_SIZEALL);
    private static readonly IntPtr _arrowCursor =
        NativeMethods.LoadCursor(IntPtr.Zero, NativeMethods.IDC_ARROW);

    private readonly AppSettings _settings;

    public WindowDragHandler(AppSettings settings)
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

    /// <summary>Called by HookManager.MouseButtonChanged before SuppressionPredicate is consulted.</summary>
    public void OnMouseButtonChanged(bool isDown)
    {
        if (isDown)
            BeginDragAttempt();
        else
            EndDrag();
    }

    /// <summary>Registered as HookManager.SuppressionPredicate. Returns true to consume the event.</summary>
    public bool ShouldSuppress(int msg)
    {
        // D-02: suppress both WM_LBUTTONDOWN and WM_LBUTTONUP while drag active
        if (_isDragging) return true;

        // D-04: swallow the click when modifier+click lands on a non-draggable surface
        // and WindowDragPassThrough=false (default). _suppressNextClick is set by BeginDragAttempt.
        if (_suppressNextClick)
        {
            if (msg == NativeMethods.WM_LBUTTONUP)
                _suppressNextClick = false;  // clear after the full pair is suppressed
            return true;
        }
        return false;
    }

    /// <summary>Called by HookManager.MouseMoved. Hot path — must be trivially fast.</summary>
    public void OnMouseMoved(Point pt)
    {
        if (!_isDragging) return;

        // Absolute-delta math: avoids accumulated drift from per-frame delta approach.
        int newX = _windowOrigin.Left + (pt.X - _dragStartCursor.X);
        int newY = _windowOrigin.Top  + (pt.Y - _dragStartCursor.Y);

        const uint SWP_FLAGS =
            NativeMethods.SWP_NOSIZE |
            NativeMethods.SWP_NOZORDER |
            NativeMethods.SWP_NOACTIVATE |
            NativeMethods.SWP_ASYNCWINDOWPOS;  // async: prevents blocking hook callback if target app is slow

        NativeMethods.SetWindowPos(_dragTarget, IntPtr.Zero, newX, newY, 0, 0, SWP_FLAGS);
    }

    private void BeginDragAttempt()
    {
        if (!_lCtrlDown || !_lAltDown) return;

        var cursorPos = System.Windows.Forms.Cursor.Position;
        var pt = new NativeMethods.POINT { X = cursorPos.X, Y = cursorPos.Y };

        // Step 1: Find window under cursor (returns child window, e.g. a TextBox)
        var rawHwnd = NativeMethods.WindowFromPoint(pt);
        if (rawHwnd == IntPtr.Zero)
        {
            // No window under cursor (desktop/taskbar) — D-04: swallow or pass through
            if (!_settings.WindowDragPassThrough) _suppressNextClick = true;
            return;
        }

        // Step 2: Walk to root top-level window (the one SetWindowPos must target)
        var rootHwnd = NativeMethods.GetAncestor(rawHwnd, NativeMethods.GA_ROOT);
        if (rootHwnd == IntPtr.Zero)
        {
            if (!_settings.WindowDragPassThrough) _suppressNextClick = true;
            return;
        }

        // Step 3: Guard — reject maximized windows (DRAG-06)
        var wp = new NativeMethods.WINDOWPLACEMENT();
        wp.length = (uint)Marshal.SizeOf<NativeMethods.WINDOWPLACEMENT>();  // MUST set before call
        if (!NativeMethods.GetWindowPlacement(rootHwnd, ref wp))
        {
            if (!_settings.WindowDragPassThrough) _suppressNextClick = true;
            return;
        }

        if (wp.showCmd == NativeMethods.SW_SHOWMAXIMIZED)
        {
            // DRAG-06: maximized window — never drag; pass through the click normally
            return;
        }

        // Step 4: Snapshot window's current screen position for absolute-delta math
        if (!NativeMethods.GetWindowRect(rootHwnd, out var rect))
            return;

        // Step 5: Commit drag state
        _dragTarget      = rootHwnd;
        _dragStartCursor = pt;
        _windowOrigin    = rect;
        _isDragging      = true;

        NativeMethods.SetCursor(_sizeAllCursor);  // D-03: visual feedback
    }

    private void EndDrag()
    {
        if (!_isDragging) return;
        _isDragging = false;
        _dragTarget = IntPtr.Zero;
        NativeMethods.SetCursor(_arrowCursor);   // D-03: restore cursor
    }

    private void CancelDragIfActive()
    {
        // Window stays at current (mid-drag) position — no position reset
        if (_isDragging)
        {
            _isDragging = false;
            _dragTarget = IntPtr.Zero;
            NativeMethods.SetCursor(_arrowCursor);
        }
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
                        if (isKeyUp)                  { _lCtrlDown = false; CancelDragIfActive(); }  // GUARD-02
                        break;
                    case NativeMethods.VK_LMENU:
                        if (isKeyDown) _lAltDown = true;
                        if (isKeyUp)   { _lAltDown = false; CancelDragIfActive(); }   // GUARD-02
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
        _isDragging        = false;
        _suppressNextClick = false;
        _lCtrlDown         = false;
        _lAltDown          = false;
        _dragTarget        = IntPtr.Zero;
    }
}
