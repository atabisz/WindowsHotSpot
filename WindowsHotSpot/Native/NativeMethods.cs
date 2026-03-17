// All P/Invoke declarations, structs, and constants for WindowsHotSpot.
// Sources: Microsoft Learn - Win32 API documentation

using System.Runtime.InteropServices;

namespace WindowsHotSpot.Native;

internal static class NativeMethods
{
    // Hook constants
    public const int WH_MOUSE_LL = 14;

    // Mouse messages
    public const int WM_MOUSEMOVE   = 0x0200;
    public const int WM_LBUTTONDOWN = 0x0201;
    public const int WM_LBUTTONUP   = 0x0202;
    public const int WM_RBUTTONDOWN = 0x0204;
    public const int WM_RBUTTONUP   = 0x0205;

    // Input constants
    public const uint INPUT_KEYBOARD  = 1;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const ushort VK_LWIN       = 0x5B;
    public const ushort VK_TAB        = 0x09;
    public const ushort VK_D          = 0x44;   // Win+D → Show Desktop (Windows 11: hides all windows)
    public const ushort VK_A          = 0x41;   // Win+A → Action Center / Quick Settings (Windows 11)

    // Delegate for low-level mouse hook callback
    public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    // Hook structs
    [StructLayout(LayoutKind.Sequential)]
    public struct POINT
    {
        public int X;
        public int Y;

        public static implicit operator System.Drawing.Point(POINT p) => new(p.X, p.Y);
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    // SendInput structs
    [StructLayout(LayoutKind.Sequential)]
    public struct INPUT
    {
        public uint type;
        public InputUnion u;
    }

    // InputUnion must include MOUSEINPUT so the union is 32 bytes on 64-bit.
    // Without it, Marshal.SizeOf<INPUT>() = 28, but Windows expects 40.
    // SendInput silently fails (returns 0) when cbSize doesn't match.
    [StructLayout(LayoutKind.Explicit)]
    public struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT mi;   // 32 bytes — determines union size
        [FieldOffset(0)] public KEYBDINPUT ki;   // 20 bytes
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MOUSEINPUT
    {
        public int dx;
        public int dy;
        public uint mouseData;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;   // 8 bytes on 64-bit; pads struct to 32 bytes total
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    // P/Invoke declarations
    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowsHookEx(int idHook,
        LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    public static extern IntPtr CallNextHookEx(IntPtr hhk,
        int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
    public static extern IntPtr GetModuleHandle(string lpModuleName);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint SendInput(uint nInputs,
        [MarshalAs(UnmanagedType.LPArray)] INPUT[] pInputs, int cbSize);

    // Single-instance IPC (SINST-01, SINST-02, SINST-03)
    public const int WM_COPYDATA = 0x004A;
    public const int SINST_SHOW_SETTINGS = 1; // lParam for WM_COPYDATA.dwData

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = false)]
    public static extern IntPtr FindWindow(string? lpClassName, string lpWindowName);

    [DllImport("user32.dll", SetLastError = false)]
    public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, ref COPYDATASTRUCT lParam);

    [StructLayout(LayoutKind.Sequential)]
    public struct COPYDATASTRUCT
    {
        public IntPtr dwData;   // message identifier (use SINST_SHOW_SETTINGS)
        public int    cbData;   // byte count of lpData (0 — we send no payload)
        public IntPtr lpData;   // pointer to data (IntPtr.Zero)
    }
}
