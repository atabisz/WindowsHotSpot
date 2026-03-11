// StartupManager: reads and writes the HKCU Run registry key for "Start with Windows".
// Uses Microsoft.Win32.Registry (inbox, no P/Invoke needed).
// Uses Environment.ProcessPath (correct for single-file publish; Assembly.Location returns "" there).

using Microsoft.Win32;

namespace WindowsHotSpot.Config;

internal static class StartupManager
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "WindowsHotSpot";

    /// <summary>
    /// Returns true if the HKCU Run key contains the WindowsHotSpot entry.
    /// </summary>
    public static bool IsEnabled
    {
        get
        {
            using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey, writable: false);
            return key?.GetValue(AppName) is not null;
        }
    }

    /// <summary>
    /// Adds or removes the HKCU Run entry for this application.
    /// The value is the quoted path to the current executable.
    /// </summary>
    public static void SetEnabled(bool enable)
    {
        using RegistryKey? key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        if (key is null) return;

        if (enable)
        {
            string exePath = $"\"{Environment.ProcessPath}\"";
            key.SetValue(AppName, exePath);
        }
        else
        {
            key.DeleteValue(AppName, throwOnMissingValue: false);
        }
    }

    /// <summary>
    /// If the Run entry exists, updates it to the current exe path.
    /// Handles the case where the exe was moved after initial registration.
    /// </summary>
    public static void RefreshPath()
    {
        if (IsEnabled)
            SetEnabled(true);
    }
}
