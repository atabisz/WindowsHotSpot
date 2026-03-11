// ActionTrigger: sends Win+Tab keystroke via SendInput to open Task View.
// Uses 4 INPUT structs sent atomically to prevent interleaving (Pattern 5).

using System.Runtime.InteropServices;
using WindowsHotSpot.Native;

namespace WindowsHotSpot.Core;

internal static class ActionTrigger
{
    /// <summary>
    /// Sends Win+Tab atomically via SendInput to trigger Task View.
    /// Must be called from the UI thread (called via WinForms Timer, not hook callback).
    /// </summary>
    public static void SendTaskView()
    {
        var inputs = new NativeMethods.INPUT[4];
        inputs[0] = MakeKeyInput(NativeMethods.VK_LWIN, keyUp: false);
        inputs[1] = MakeKeyInput(NativeMethods.VK_TAB,  keyUp: false);
        inputs[2] = MakeKeyInput(NativeMethods.VK_TAB,  keyUp: true);
        inputs[3] = MakeKeyInput(NativeMethods.VK_LWIN, keyUp: true);
        NativeMethods.SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<NativeMethods.INPUT>());
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
