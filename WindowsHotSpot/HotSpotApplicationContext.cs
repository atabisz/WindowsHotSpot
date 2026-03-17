// HotSpotApplicationContext: owns all component lifetimes and provides the system tray interface.
// Pattern: ApplicationContext with no MainForm -> no taskbar button (TRAY-01).
// Wires HookManager -> CornerDetector for hot corner detection.
// Phase 2: ConfigManager loaded on startup; SettingsChanged wired to CornerDetector.UpdateSettings.
// Phase 1.1: IpcWindow hidden message-only window receives WM_COPYDATA for single-instance guard.

using System.Runtime.InteropServices;
using WindowsHotSpot.Config;
using WindowsHotSpot.Core;
using WindowsHotSpot.Native;
using WindowsHotSpot.UI;

namespace WindowsHotSpot;

internal sealed class HotSpotApplicationContext : ApplicationContext
{
    private readonly ConfigManager _configManager;
    private readonly HookManager _hookManager;
    private readonly CornerDetector _cornerDetector;
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly IpcWindow _ipcWindow;

    public HotSpotApplicationContext()
    {
        // Load settings (or defaults if settings.json missing/corrupt)
        _configManager = new ConfigManager();
        _configManager.Load();

        // Build context menu (TRAY-03)
        _contextMenu = new ContextMenuStrip();

        var settingsItem = new ToolStripMenuItem("Settings");
        settingsItem.Click += OnSettingsClick;

        var aboutItem = new ToolStripMenuItem("About");
        aboutItem.Click += OnAboutClick;

        var quitItem = new ToolStripMenuItem("Quit");
        quitItem.Click += OnQuitClick;

        _contextMenu.Items.Add(settingsItem);
        _contextMenu.Items.Add(aboutItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(quitItem);

        // Create system tray icon (TRAY-02) — loaded from embedded resource.
        // Icon(Stream, int, int) selects the closest size frame from the ICO.
        // PNG-compressed ICO frames are supported natively on .NET 10 / Windows Vista+.
        // Stream is intentionally not disposed — manifest resource streams are backed by
        // process-mapped assembly memory and the Icon reads lazily on first Handle access.
        var iconStream = typeof(HotSpotApplicationContext).Assembly
            .GetManifestResourceStream("WindowsHotSpot.Resources.app.ico")
            ?? throw new InvalidOperationException(
                "Embedded icon resource 'WindowsHotSpot.Resources.app.ico' not found. " +
                "Verify <EmbeddedResource Include=\"Resources\\app.ico\" /> in WindowsHotSpot.csproj.");
        _trayIcon = new NotifyIcon
        {
            Icon = new Icon(iconStream, 16, 16),
            Text = "WindowsHotSpot",
            ContextMenuStrip = _contextMenu,
            Visible = true
        };

        // Create detection components using loaded settings
        _cornerDetector = new CornerDetector(
            _configManager.Settings.Corner,
            _configManager.Settings.ZoneSize,
            _configManager.Settings.DwellDelayMs);

        _hookManager = new HookManager();

        _hookManager.MouseMoved += _cornerDetector.OnMouseMoved;
        _hookManager.MouseButtonChanged += _cornerDetector.OnMouseButtonChanged;

        // Live settings propagation: SettingsChanged -> UpdateSettings (SETT-05)
        _configManager.SettingsChanged += () => _cornerDetector.UpdateSettings(
            _configManager.Settings.Corner,
            _configManager.Settings.ZoneSize,
            _configManager.Settings.DwellDelayMs);

        // Belt-and-suspenders cleanup on application exit (CORE-06)
        Application.ApplicationExit += OnApplicationExit;

        // Install the global mouse hook
        _hookManager.Install();

        // Single-instance IPC receiver (SINST-02)
        _ipcWindow = new IpcWindow();
        _ipcWindow.ShowSettingsRequested += ShowSettingsWindow;
    }

    // TRAY-04: Opens SettingsForm modal; delegates to ShowSettingsWindow (SETT-01..04, SINST-02)
    private void OnSettingsClick(object? sender, EventArgs e) => ShowSettingsWindow();

    // Shows the Settings window, bringing it to front if already open (SINST-02, TRAY-04)
    private void ShowSettingsWindow()
    {
        // If a Settings form is already open, bring it to front instead of opening a second
        foreach (Form f in Application.OpenForms)
        {
            if (f is SettingsForm)
            {
                f.BringToFront();
                f.Activate();
                return;
            }
        }

        using var form = new SettingsForm(_configManager.Settings);
        if (form.ShowDialog() == DialogResult.OK)
        {
            _configManager.Settings.Corner = form.SelectedCorner;
            _configManager.Settings.ZoneSize = form.SelectedZoneSize;
            _configManager.Settings.DwellDelayMs = form.SelectedDwellDelay;
            _configManager.Settings.StartWithWindows = form.SelectedStartWithWindows;
            StartupManager.SetEnabled(form.SelectedStartWithWindows);
            _configManager.Save(); // Fires SettingsChanged -> CornerDetector.UpdateSettings
        }
    }

    // TRAY-05: About dialog with app name, version, description
    private void OnAboutClick(object? sender, EventArgs e)
    {
        var version = typeof(HotSpotApplicationContext).Assembly
            .GetName().Version?.ToString(3) ?? "unknown";
        MessageBox.Show(
            $"WindowsHotSpot v{version}\n\nMacOS-style hot corners for Windows.\nMove your mouse to a screen corner to trigger Task View.",
            "About WindowsHotSpot",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    // TRAY-06: Quit cleanly exits and removes tray icon
    private void OnQuitClick(object? sender, EventArgs e)
    {
        ExitThread();
    }

    private void OnApplicationExit(object? sender, EventArgs e)
    {
        DisposeComponents();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DisposeComponents();
        }
        base.Dispose(disposing);
    }

    private void DisposeComponents()
    {
        _ipcWindow.ShowSettingsRequested -= ShowSettingsWindow;
        _ipcWindow.Dispose();

        _hookManager.MouseMoved -= _cornerDetector.OnMouseMoved;
        _hookManager.MouseButtonChanged -= _cornerDetector.OnMouseButtonChanged;

        _hookManager.Dispose();
        _cornerDetector.Dispose();

        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _contextMenu.Dispose();
    }

    /// <summary>
    /// Minimal hidden message-only window used as single-instance IPC target.
    /// Receives WM_COPYDATA from a second instance and notifies the app context.
    /// </summary>
    private sealed class IpcWindow : NativeWindow, IDisposable
    {
        public event Action? ShowSettingsRequested;

        public IpcWindow()
        {
            // Message-only window: HWND_MESSAGE parent makes it invisible and off the
            // Alt+Tab/taskbar list. The window title is used by FindWindow in Program.cs.
            var cp = new CreateParams
            {
                Caption = "WindowsHotSpot_IPCTarget",
                Parent = new IntPtr(-3), // HWND_MESSAGE
            };
            CreateHandle(cp);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == NativeMethods.WM_COPYDATA)
            {
                var cds = (NativeMethods.COPYDATASTRUCT)
                    Marshal.PtrToStructure(
                        m.LParam, typeof(NativeMethods.COPYDATASTRUCT))!;

                if (cds.dwData.ToInt32() == NativeMethods.SINST_SHOW_SETTINGS)
                {
                    ShowSettingsRequested?.Invoke();
                }
                m.Result = new IntPtr(1); // tell sender the message was handled
                return;
            }
            base.WndProc(ref m);
        }

        public void Dispose() => DestroyHandle();
    }
}
