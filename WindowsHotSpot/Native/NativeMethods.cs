// All P/Invoke declarations, structs, and constants for WindowsHotSpot.
// Sources: Microsoft Learn - Win32 API documentation

using System.Runtime.InteropServices;

namespace WindowsHotSpot.Native;

internal static class NativeMethods
{
    // Hook constants
    public const int WH_MOUSE_LL = 14;
    public const int WH_KEYBOARD_LL = 13;

    // Keyboard messages (used in WH_KEYBOARD_LL hook wParam)
    public const int WM_KEYDOWN    = 0x0100;
    public const int WM_KEYUP      = 0x0101;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_SYSKEYUP   = 0x0105;

    // Mouse messages
    public const int WM_MOUSEMOVE    = 0x0200;
    public const int WM_LBUTTONDOWN  = 0x0201;
    public const int WM_LBUTTONUP    = 0x0202;
    public const int WM_LBUTTONDBLCLK = 0x0203;  // synthesized by window mgr — documents the message code; not seen in WH_MOUSE_LL
    public const int WM_RBUTTONDOWN  = 0x0204;
    public const int WM_RBUTTONUP    = 0x0205;
    public const int WM_MOUSEWHEEL   = 0x020A;

    // Input constants
    public const uint INPUT_KEYBOARD  = 1;
    public const uint KEYEVENTF_KEYUP = 0x0002;
    public const ushort VK_LWIN       = 0x5B;
    public const ushort VK_RWIN       = 0x5C;
    public const ushort VK_TAB        = 0x09;
    public const ushort VK_D          = 0x44;   // Win+D → Show Desktop (Windows 11: hides all windows)
    public const ushort VK_A          = 0x41;   // Win+A → Action Center / Quick Settings (Windows 11)

    // Virtual key codes for left/right modifier detection (WH_KEYBOARD_LL)
    public const uint VK_LCONTROL = 0xA2;
    public const uint VK_RCONTROL = 0xA3;
    public const uint VK_LMENU    = 0xA4;
    public const uint VK_RMENU    = 0xA5;
    public const uint VK_LSHIFT   = 0xA0;

    // KBDLLHOOKSTRUCT.flags bit: event was injected (AltGr fake LCtrl guard — GUARD-01)
    public const uint LLKHF_INJECTED = 0x10;

    // OpenProcess access right — sufficient to query basic process info without elevation
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    // GetAncestor flags
    public const uint GA_ROOT = 2;

    // SetWindowPos flags
    public const uint SWP_NOSIZE         = 0x0001;
    public const uint SWP_NOMOVE         = 0x0002;
    public const uint SWP_NOZORDER       = 0x0004;
    public const uint SWP_NOACTIVATE     = 0x0010;
    public const uint SWP_ASYNCWINDOWPOS = 0x4000;

    // SetWindowPos hWndInsertAfter sentinel values
    public static readonly IntPtr HWND_TOPMOST   = new(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new(-2);

    // GetWindowLong index and extended style flags
    public const int  GWL_EXSTYLE   = -20;
    public const uint WS_EX_TOPMOST = 0x00000008;
    public const uint WS_EX_LAYERED = 0x00080000;
    public const uint LWA_COLORKEY  = 0x00000001;
    public const uint LWA_ALPHA     = 0x00000002;

    // WINDOWPLACEMENT.showCmd values
    public const uint SW_SHOWMAXIMIZED = 3;

    // System cursor identifiers (MAKEINTRESOURCE values — shared resources, never call DestroyCursor)
    public static readonly IntPtr IDC_ARROW   = new IntPtr(32512);
    public static readonly IntPtr IDC_SIZEALL = new IntPtr(32646);

    // System metrics indices for double-click detection
    public const int SM_CXDOUBLECLK = 36;
    public const int SM_CYDOUBLECLK = 37;
    // System metrics indices for minimum window tracking size (RESIZE-04)
    public const int SM_CXMINTRACK  = 34;   // minimum tracking width
    public const int SM_CYMINTRACK  = 35;   // minimum tracking height

    // Delegate for low-level mouse hook callback
    public delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);

    // Delegate for low-level keyboard hook callback (same signature as mouse — distinct type for type-safety)
    public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

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

    [StructLayout(LayoutKind.Sequential)]
    public struct KBDLLHOOKSTRUCT
    {
        public uint   vkCode;
        public uint   scanCode;
        public uint   flags;
        public uint   time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPLACEMENT
    {
        public uint  length;        // must be set to Marshal.SizeOf<WINDOWPLACEMENT>() before call
        public uint  flags;
        public uint  showCmd;       // SW_SHOWMAXIMIZED = 3 when window is maximized
        public POINT ptMinPosition;
        public POINT ptMaxPosition;
        public RECT  rcNormalPosition;
        public RECT  rcDevice;      // Windows 10+
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
    public static extern IntPtr SetWindowsHookEx(int idHook,
        LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

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

    // Window dragging APIs
    [DllImport("user32.dll")]
    public static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll")]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);

    [DllImport("advapi32.dll", SetLastError = true)]
    public static extern bool GetTokenInformation(IntPtr TokenHandle, uint TokenInformationClass,
        out uint TokenInformation, uint TokenInformationLength, out uint ReturnLength);

    public const uint TOKEN_QUERY              = 0x0008;
    public const uint TokenElevation           = 20;

    [DllImport("user32.dll")]
    public static extern IntPtr WindowFromPoint(POINT Point);

    [DllImport("user32.dll")]
    public static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    public static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    public static extern uint GetCurrentThreadId();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetLayeredWindowAttributes(
        IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetLayeredWindowAttributes(
        IntPtr hwnd, out uint pcrKey, out byte pbAlpha, out uint pdwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    public static extern IntPtr LoadCursor(IntPtr hInstance, IntPtr lpCursorName);

    [DllImport("user32.dll")]
    public static extern IntPtr SetCursor(IntPtr hCursor);

    [DllImport("user32.dll")]
    public static extern uint GetDoubleClickTime();

    [DllImport("user32.dll")]
    public static extern int GetSystemMetrics(int nIndex);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern int GetWindowText(IntPtr hWnd, System.Text.StringBuilder lpString, int nMaxCount);
}
