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

    /// <summary>
    /// Optional predicate consulted for WM_LBUTTONDOWN and WM_LBUTTONUP only.
    /// Return true to suppress the event (hook proc returns 1; target window does not receive it).
    /// Return false or leave null to pass the event through normally.
    /// WM_MOUSEMOVE is never suppressed regardless of this predicate (HOOK-02).
    /// IMPORTANT: This predicate is called on the UI thread inside the hook callback.
    /// It must return immediately — any I/O or blocking will silently kill the hook (Pitfall 1).
    /// IMPORTANT: MouseButtonChanged fires BEFORE this predicate is consulted.
    /// The consumer's event handler runs first, updating consumer state, so the predicate
    /// can make the correct suppress/pass decision based on freshly updated state.
    /// </summary>
    public Func<int, bool>? SuppressionPredicate { get; set; }

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
                // WM_MOUSEMOVE: SuppressionPredicate is NEVER consulted (HOOK-02)
            }
            else if (msg == NativeMethods.WM_LBUTTONDOWN || msg == NativeMethods.WM_LBUTTONUP)
            {
                // Fire event FIRST so consumer state is updated before predicate runs (Pitfall 2)
                MouseButtonChanged?.Invoke(msg == NativeMethods.WM_LBUTTONDOWN);
                if (SuppressionPredicate?.Invoke(msg) == true)
                    return new IntPtr(1); // consumed — target window does not receive this event
            }
            else if (msg == NativeMethods.WM_RBUTTONDOWN || msg == NativeMethods.WM_RBUTTONUP)
            {
                MouseButtonChanged?.Invoke(msg == NativeMethods.WM_RBUTTONDOWN);
                // Right-button: SuppressionPredicate NEVER consulted (HOOK-02)
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
