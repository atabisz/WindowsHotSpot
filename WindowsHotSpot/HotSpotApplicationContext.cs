// HotSpotApplicationContext: owns all component lifetimes and provides the system tray interface.
// Pattern: ApplicationContext with no MainForm -> no taskbar button (TRAY-01).
// Wires HookManager -> CornerRouter for hot corner detection.
// Phase 2: ConfigManager loaded on startup; SettingsChanged wired to CornerDetector.UpdateSettings.
// Phase 1.1: IpcWindow hidden message-only window receives WM_COPYDATA for single-instance guard.
// Phase 3: CornerDetector replaced by CornerRouter; one detector per (monitor, enabled corner) pair.
//           DisplaySettingsChanged subscription for live monitor change support.

using System.Runtime.InteropServices;
using Microsoft.Win32;
using WindowsHotSpot.Config;
using WindowsHotSpot.Core;
using WindowsHotSpot.Native;
using WindowsHotSpot.UI;

namespace WindowsHotSpot;

internal sealed class HotSpotApplicationContext : ApplicationContext
{
    private readonly ConfigManager _configManager;
    private readonly HookManager _hookManager;
    private readonly CornerRouter _cornerRouter;
    private readonly WindowDragHandler _windowDragHandler;
    private readonly ScrollResizeHandler _scrollResizeHandler;
    private readonly AlwaysOnTopHandler _alwaysOnTopHandler;
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly IpcWindow _ipcWindow;
    private bool _disposed;
    private readonly Action _onSettingsChanged;

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

        // Build detection pipeline. CornerRouter owns one CornerDetector per active (monitor, corner) pair.
        _cornerRouter = new CornerRouter();
        _cornerRouter.Rebuild(_configManager.Settings);

        _hookManager = new HookManager();

        _hookManager.MouseMoved += _cornerRouter.OnMouseMoved;
        _hookManager.MouseButtonChanged += _cornerRouter.OnMouseButtonChanged;

        _windowDragHandler = new WindowDragHandler(_configManager.Settings);
        _hookManager.MouseMoved += _windowDragHandler.OnMouseMoved;
        _hookManager.MouseButtonChanged += _windowDragHandler.OnMouseButtonChanged;
        _hookManager.SuppressionPredicate = _windowDragHandler.ShouldSuppress;
        _windowDragHandler.Install();

        _scrollResizeHandler = new ScrollResizeHandler(_configManager.Settings);
        _hookManager.MouseWheeled += _scrollResizeHandler.OnMouseWheeled;
        _scrollResizeHandler.Install();

        _alwaysOnTopHandler = new AlwaysOnTopHandler(_configManager.Settings, _trayIcon);
        _hookManager.MouseButtonChanged += _alwaysOnTopHandler.OnMouseButtonChanged;
        _alwaysOnTopHandler.Install();

        // Live settings propagation: rebuild detector pool when settings change (SETT-05, MMON-01)
        _onSettingsChanged = () => _cornerRouter.Rebuild(_configManager.Settings);
        _configManager.SettingsChanged += _onSettingsChanged;

        // Monitor plug/unplug: rebuild detector pool to pick up the new display topology (MMON-02).
        // CRITICAL: static event -- must unsubscribe in DisposeComponents() to prevent memory leak.
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;

        // Belt-and-suspenders cleanup on application exit (CORE-06)
        Application.ApplicationExit += OnApplicationExit;

        // Install the global mouse hook
        _hookManager.Install();

        // Single-instance IPC receiver (SINST-02)
        _ipcWindow = new IpcWindow();
        _ipcWindow.ShowSettingsRequested += ShowSettingsWindow;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e)
        => _cornerRouter.Rebuild(_configManager.Settings);

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
            // Apply global detection parameters
            _configManager.Settings.ZoneSize = form.SelectedZoneSize;
            _configManager.Settings.DwellDelayMs = form.SelectedDwellDelay;
            _configManager.Settings.StartWithWindows = form.SelectedStartWithWindows;
            _configManager.Settings.WindowDragPassThrough = form.SelectedWindowDragPassThrough;
            _configManager.Settings.ScrollResizeStep = form.SelectedScrollResizeStep;
            StartupManager.SetEnabled(form.SelectedStartWithWindows);

            // Apply per-monitor corner configs (including custom shortcuts).
            // Merge into existing MonitorConfigs so disconnected-monitor data is preserved (MMON-04).
            foreach (var (deviceName, config) in form.GetMonitorConfigs())
                _configManager.Settings.MonitorConfigs[deviceName] = config;

            _configManager.Settings.SameOnAllMonitors = form.SelectedSameOnAllMonitors;

            _configManager.Save(); // Fires SettingsChanged -> CornerRouter.Rebuild
        }
    }

    // TRAY-05: About dialog with app name, version, description
    private void OnAboutClick(object? sender, EventArgs e)
    {
        var version = typeof(HotSpotApplicationContext).Assembly
            .GetName().Version?.ToString(3) ?? "unknown";
        MessageBox.Show(
            $"WindowsHotSpot v{version}\n\nMacOS-style hot corners for Windows.\nMove your mouse to a configured screen corner to trigger an action.",
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
        if (_disposed) return;
        _disposed = true;

        _ipcWindow.ShowSettingsRequested -= ShowSettingsWindow;
        _ipcWindow.Dispose();

        // Unsubscribe static event FIRST to prevent memory leak (Pitfall 3 in research).
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;

        _hookManager.MouseMoved -= _cornerRouter.OnMouseMoved;
        _hookManager.MouseButtonChanged -= _cornerRouter.OnMouseButtonChanged;

        _hookManager.MouseMoved -= _windowDragHandler.OnMouseMoved;
        _hookManager.MouseButtonChanged -= _windowDragHandler.OnMouseButtonChanged;
        _hookManager.SuppressionPredicate = null;
        _windowDragHandler.Dispose();

        _hookManager.MouseWheeled -= _scrollResizeHandler.OnMouseWheeled;
        _scrollResizeHandler.Dispose();

        _hookManager.MouseButtonChanged -= _alwaysOnTopHandler.OnMouseButtonChanged;
        _alwaysOnTopHandler.Dispose();

        _hookManager.Dispose();
        _configManager.SettingsChanged -= _onSettingsChanged;
        _cornerRouter.Dispose();

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
