// HotSpotApplicationContext: owns all component lifetimes and provides the system tray interface.
// Pattern: ApplicationContext with no MainForm -> no taskbar button (TRAY-01).
// Wires HookManager -> CornerDetector for hot corner detection.
// Phase 2: ConfigManager loaded on startup; SettingsChanged wired to CornerDetector.UpdateSettings.

using WindowsHotSpot.Config;
using WindowsHotSpot.Core;
using WindowsHotSpot.UI;

namespace WindowsHotSpot;

internal sealed class HotSpotApplicationContext : ApplicationContext
{
    private readonly ConfigManager _configManager;
    private readonly HookManager _hookManager;
    private readonly CornerDetector _cornerDetector;
    private readonly NotifyIcon _trayIcon;
    private readonly ContextMenuStrip _contextMenu;

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

        // Create system tray icon (TRAY-02)
        // No app.ico available (ImageMagick not present); using SystemIcons.Application as fallback.
        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
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
    }

    // TRAY-04: Opens SettingsForm modal; on OK, persists changes and applies them live (SETT-01..04)
    private void OnSettingsClick(object? sender, EventArgs e)
    {
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
