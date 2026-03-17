// ActionDispatcher: routes a CornerAction value to the correct SendInput sequence.
// Called from CornerDetector.OnDwellComplete on the UI thread (WinForms Timer) — safe for SendInput.
//
// CRITICAL: SendInput cbSize MUST use Marshal.SizeOf<NativeMethods.INPUT>().
// Hardcoding 28 or 40, or using sizeof(), causes SendInput to silently return 0.
// (See MEMORY.md: "Critical Bug Fixed" — InputUnion must include MOUSEINPUT for correct cbSize.)
//
// Win+A = Action Center / Quick Settings on Windows 11.
// Win+D = Show Desktop on all Windows versions.

using System.Runtime.InteropServices;
using WindowsHotSpot.Config;
using WindowsHotSpot.Native;

namespace WindowsHotSpot.Core;

internal static class ActionDispatcher
{
    /// <summary>
    /// Dispatches the action for the dwelled corner. No-op for Disabled.
    /// Must be called from the UI thread (WinForms Timer tick, not hook callback).
    /// </summary>
    public static void Dispatch(CornerAction action)
    {
        switch (action)
        {
            case CornerAction.TaskView:
                ActionTrigger.SendTaskView();   // reuses existing Win+Tab logic
                break;
            case CornerAction.ShowDesktop:
                SendWinKey(NativeMethods.VK_D);
                break;
            case CornerAction.ActionCenter:
                SendWinKey(NativeMethods.VK_A);
                break;
            case CornerAction.Disabled:
                break;   // intentional no-op
        }
    }

    // Sends Win+<vk> atomically: LWin↓, vk↓, vk↑, LWin↑.
    // 4-struct atomic send matches the pattern in ActionTrigger.SendTaskView().
    private static void SendWinKey(ushort vk)
    {
        var inputs = new NativeMethods.INPUT[4];
        inputs[0] = MakeKeyInput(NativeMethods.VK_LWIN, keyUp: false);
        inputs[1] = MakeKeyInput(vk,                    keyUp: false);
        inputs[2] = MakeKeyInput(vk,                    keyUp: true);
        inputs[3] = MakeKeyInput(NativeMethods.VK_LWIN, keyUp: true);
        NativeMethods.SendInput(
            (uint)inputs.Length,
            inputs,
            Marshal.SizeOf<NativeMethods.INPUT>());   // MUST be Marshal.SizeOf — see comment above
    }

    private static NativeMethods.INPUT MakeKeyInput(ushort vk, bool keyUp)
    {
        return new NativeMethods.INPUT
        {
            type = NativeMethods.INPUT_KEYBOARD,
            u = new NativeMethods.InputUnion
            {
                ki = new NativeMethods.KEYBDINPUT
                {
                    wVk = vk,
                    dwFlags = keyUp ? NativeMethods.KEYEVENTF_KEYUP : 0
                }
            }
        };
    }
}
