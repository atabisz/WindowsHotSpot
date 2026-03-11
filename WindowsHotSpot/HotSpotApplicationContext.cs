// HotSpotApplicationContext: owns all component lifetimes and provides the system tray interface.
// Pattern: ApplicationContext with no MainForm -> no taskbar button (TRAY-01).
// Wires HookManager -> CornerDetector for hot corner detection.

using WindowsHotSpot.Core;

namespace WindowsHotSpot;

internal sealed class HotSpotApplicationContext : ApplicationContext
{
    private readonly HookManager _hookManager;
    private readonly CornerDetector _cornerDetector;
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _contextMenu;

    public HotSpotApplicationContext()
    {
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

        // Create system tray icon (TRAY-02)
        // No app.ico available (ImageMagick not present); using SystemIcons.Application as fallback.
        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "WindowsHotSpot",
            ContextMenuStrip = _contextMenu,
            Visible = true
        };

        // Create detection components and wire events
        _cornerDetector = new CornerDetector();
        _hookManager = new HookManager();

        _hookManager.MouseMoved += _cornerDetector.OnMouseMoved;
        _hookManager.MouseButtonChanged += _cornerDetector.OnMouseButtonChanged;

        // Belt-and-suspenders cleanup on application exit (CORE-06)
        Application.ApplicationExit += OnApplicationExit;

        // Install the global mouse hook
        _hookManager.Install();
    }

    // TRAY-04: Settings placeholder (Phase 2 will show SettingsForm)
    private void OnSettingsClick(object? sender, EventArgs e)
    {
        MessageBox.Show(
            "Settings will be available in a future update.",
            "Settings",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    // TRAY-05: About dialog with app name, version, description
    private void OnAboutClick(object? sender, EventArgs e)
    {
        MessageBox.Show(
            "WindowsHotSpot v0.1.0\n\nMacOS-style hot corners for Windows.\nMove your mouse to a screen corner to trigger Task View.",
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
        _hookManager.MouseMoved -= _cornerDetector.OnMouseMoved;
        _hookManager.MouseButtonChanged -= _cornerDetector.OnMouseButtonChanged;

        _hookManager.Dispose();
        _cornerDetector.Dispose();

        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        _contextMenu.Dispose();
    }
}
