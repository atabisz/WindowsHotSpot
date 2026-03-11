// WindowsHotSpot - macOS-style hot corners for Windows
// Entry point: STA thread, WinForms message loop, ApplicationContext

namespace WindowsHotSpot;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

        // Belt-and-suspenders: ensure hook is cleaned up on unhandled exceptions
        AppDomain.CurrentDomain.UnhandledException += (_, _) =>
        {
            Environment.Exit(1);
        };

        Application.Run(new HotSpotApplicationContext());
    }
}
