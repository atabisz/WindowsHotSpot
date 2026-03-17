// WindowsHotSpot - macOS-style hot corners for Windows
// Entry point: STA thread, WinForms message loop, ApplicationContext

using WindowsHotSpot.Native;

namespace WindowsHotSpot;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        // Single-instance guard (SINST-01, SINST-03, SINST-04)
        // Mutex name is user-scoped (Local\) so separate Windows sessions don't block each other.
        using var mutex = new Mutex(initiallyOwned: true,
            name: "Local\\WindowsHotSpot_SingleInstance",
            out bool createdNew);

        if (!createdNew)
        {
            // Second instance: signal the first, then exit silently (SINST-02, SINST-03)
            var hwnd = NativeMethods.FindWindow(null, "WindowsHotSpot_IPCTarget");
            if (hwnd != IntPtr.Zero)
            {
                var cds = new NativeMethods.COPYDATASTRUCT
                {
                    dwData = new IntPtr(NativeMethods.SINST_SHOW_SETTINGS),
                    cbData = 0,
                    lpData = IntPtr.Zero,
                };
                NativeMethods.SendMessage(hwnd, NativeMethods.WM_COPYDATA, IntPtr.Zero, ref cds);
            }
            return; // exits silently — no dialog, no Environment.Exit needed (SINST-03)
        }

        // Belt-and-suspenders: ensure hook is cleaned up on unhandled exceptions
        AppDomain.CurrentDomain.UnhandledException += (_, _) =>
        {
            Environment.Exit(1);
        };

        Application.Run(new HotSpotApplicationContext());
        // Mutex is released here by the `using` disposal (SINST-04)
    }
}
