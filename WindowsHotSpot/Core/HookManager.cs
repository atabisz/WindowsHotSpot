// HookManager: installs and manages the global WH_MOUSE_LL low-level mouse hook.
// CRITICAL: _hookCallback stored as class-level field to prevent GC collection (Pitfall 2).
// CRITICAL: HookCallback must return in <300ms or Windows silently removes the hook (Pitfall 1).

using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using WindowsHotSpot.Native;

namespace WindowsHotSpot.Core;

internal sealed class HookManager : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;

    // MUST be a class-level field -- prevents GC from collecting the delegate
    // while the hook is installed. A local variable or lambda would be collected.
    private readonly NativeMethods.LowLevelMouseProc _hookCallback;

    /// <summary>Fired on WM_MOUSEMOVE with the current cursor position in physical pixels.</summary>
    public event Action<Point>? MouseMoved;

    /// <summary>Fired on button down (true) or button up (false) for left and right buttons.</summary>
    public event Action<bool>? MouseButtonChanged;

    public HookManager()
    {
        _hookCallback = HookCallback;
    }

    /// <summary>Installs the global low-level mouse hook. Throws Win32Exception on failure.</summary>
    public void Install()
    {
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        using var module = process.MainModule!;
        _hookId = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL,
            _hookCallback,
            NativeMethods.GetModuleHandle(module.ModuleName!),
            0);

        if (_hookId == IntPtr.Zero)
            throw new Win32Exception(Marshal.GetLastWin32Error(), "Failed to install mouse hook.");
    }

    // Hook callback: MUST be trivially fast (Pitfall 1).
    // Only reads MSLLHOOKSTRUCT, fires event, calls CallNextHookEx.
    // No allocation, no I/O, no logging.
    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            if (msg == NativeMethods.WM_MOUSEMOVE)
            {
                var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                MouseMoved?.Invoke(hookStruct.pt);
            }
            else if (msg == NativeMethods.WM_LBUTTONDOWN || msg == NativeMethods.WM_RBUTTONDOWN)
            {
                MouseButtonChanged?.Invoke(true);
            }
            else if (msg == NativeMethods.WM_LBUTTONUP || msg == NativeMethods.WM_RBUTTONUP)
            {
                MouseButtonChanged?.Invoke(false);
            }
        }
        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }
}
