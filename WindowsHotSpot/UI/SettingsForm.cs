// SettingsForm: redesigned modal settings dialog for WindowsHotSpot.
// Phase 4: 2x2 corner grid per monitor, monitor selector for multi-monitor setups,
// Record buttons wired to KeyRecorderPanel for custom shortcut capture.
//
// Manual layout (no designer file) for diffability.
// AutoScaleMode.Dpi prevents blurry controls on high-DPI displays (research Pitfall 5).
// ShowDialog() is safe from the hook thread: it runs its own nested message loop that
// still dispatches WH_MOUSE_LL callbacks (research Pitfall 4).

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using WindowsHotSpot.Config;

namespace WindowsHotSpot.UI;

internal sealed class SettingsForm : Form
{
    // ── Public API for HotSpotApplicationContext ──────────────────────────
    public Dictionary<string, MonitorCornerConfig> GetMonitorConfigs()
    {
        if (_sameOnAllMonitorsCheckBox?.Checked != true)
            return _pendingMonitorConfigs;

        // Replicate the current single config to all connected monitors
        if (!_pendingMonitorConfigs.TryGetValue(_selectedDeviceName, out var baseConfig))
            return _pendingMonitorConfigs;

        var result = new Dictionary<string, MonitorCornerConfig>();
        foreach (var screen in _screens)
        {
            result[screen.DeviceName] = new MonitorCornerConfig
            {
                CornerActions = new Dictionary<HotCorner, CornerAction>(baseConfig.CornerActions),
                CustomShortcuts = new Dictionary<HotCorner, CustomShortcut>(baseConfig.CustomShortcuts),
            };
        }
        return result;
    }

    public int SelectedZoneSize => (int)_zoneSizeInput.Value;
    public int SelectedDwellDelay => (int)_dwellDelayInput.Value;
    public bool SelectedStartWithWindows => _startupCheckBox.Checked;
    public bool SelectedSameOnAllMonitors => _sameOnAllMonitorsCheckBox?.Checked ?? false;
    public bool SelectedWindowDragPassThrough => _windowDragPassThroughCheckBox.Checked;

    // ── State ─────────────────────────────────────────────────────────────
    private readonly Screen[] _screens;
    private string _selectedDeviceName;
    private readonly Dictionary<string, MonitorCornerConfig> _pendingMonitorConfigs;
    private bool _anyPanelRecording;

    // ── Global controls ───────────────────────────────────────────────────
    private readonly ComboBox _monitorCombo;
    private readonly GroupBox _monitorGroup;
    private readonly CheckBox? _sameOnAllMonitorsCheckBox;
    private readonly NumericUpDown _zoneSizeInput;
    private readonly NumericUpDown _dwellDelayInput;
    private readonly CheckBox _startupCheckBox;
    private readonly CheckBox _windowDragPassThroughCheckBox;
    private readonly Button _saveButton;
    private readonly Button _cancelButton;

    // ── Per-corner controls (indexed by HotCorner int value) ──────────────
    // Corner order: TopLeft=0, TopRight=1, BottomLeft=2, BottomRight=3
    private readonly ComboBox[] _actionCombos = new ComboBox[4];
    private readonly KeyRecorderPanel[] _recorderPanels = new KeyRecorderPanel[4];
    private readonly Button[] _recordButtons = new Button[4];

    // Maps ComboBox display string to CornerAction enum
    private static readonly (string Label, CornerAction Action)[] ActionItems =
    [
        ("Disabled",         CornerAction.Disabled),
        ("Task View",        CornerAction.TaskView),
        ("Show Desktop",     CornerAction.ShowDesktop),
        ("Action Center",    CornerAction.ActionCenter),
        ("Custom Shortcut",  CornerAction.Custom),
    ];

    public SettingsForm(AppSettings settings)
    {
        SuspendLayout();

        Text = "WindowsHotSpot — Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        ClientSize = new Size(420, 500);
        Padding = new Padding(0);
        BackColor = SystemColors.Window;

        // ── Snapshot screens and build pending config dictionary ──────────
        _screens = Screen.AllScreens;

        _pendingMonitorConfigs = new Dictionary<string, MonitorCornerConfig>();
        foreach (var screen in _screens)
        {
            // Clone existing config or create new defaults — do not mutate caller's object
            if (settings.MonitorConfigs.TryGetValue(screen.DeviceName, out var existing))
            {
                var clone = new MonitorCornerConfig
                {
                    CornerActions = new Dictionary<HotCorner, CornerAction>(existing.CornerActions),
                    CustomShortcuts = new Dictionary<HotCorner, CustomShortcut>(existing.CustomShortcuts),
                };
                _pendingMonitorConfigs[screen.DeviceName] = clone;
            }
            else
            {
                _pendingMonitorConfigs[screen.DeviceName] = new MonitorCornerConfig();
            }
        }

        _selectedDeviceName = _screens[0].DeviceName;

        // ── Monitor selector group (hidden when single monitor) ───────────
        _monitorGroup = new GroupBox
        {
            Text = "Monitor",
            Location = new Point(12, 12),
            Size = new Size(396, 76),
        };

        _monitorCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(12, 20),
            Width = 368,
        };
        foreach (var screen in _screens)
        {
            string label = screen.Primary
                ? $"{screen.DeviceName} (primary)"
                : screen.DeviceName;
            _monitorCombo.Items.Add(label);
        }
        _monitorCombo.SelectedIndex = 0;
        _monitorCombo.SelectedIndexChanged += OnMonitorChanged;

        _monitorGroup.Controls.Add(_monitorCombo);

        if (_screens.Length > 1)
        {
            _sameOnAllMonitorsCheckBox = new CheckBox
            {
                Text = "Same on all monitors",
                Location = new Point(12, 48),
                AutoSize = true,
                Checked = settings.SameOnAllMonitors,
            };
            _sameOnAllMonitorsCheckBox.CheckedChanged += (_, _) =>
            {
                bool sameForAll = _sameOnAllMonitorsCheckBox.Checked;
                _monitorCombo.Visible = !sameForAll;
                if (sameForAll)
                {
                    // Flush current grid to the selected device before switching to single-config mode
                    SaveGridToConfig(_selectedDeviceName);
                }
            };
            _monitorGroup.Controls.Add(_sameOnAllMonitorsCheckBox);

            // Apply initial visibility state
            _monitorCombo.Visible = !settings.SameOnAllMonitors;
        }

        // Hide monitor selector when only one screen — Pitfall 6 prevention
        _monitorGroup.Visible = _screens.Length > 1;

        // ── Corner grid group ─────────────────────────────────────────────
        int cornerGroupTop = _monitorGroup.Visible ? 96 : 12;

        var cornerGroup = new GroupBox
        {
            Text = "Corner Actions",
            Location = new Point(12, cornerGroupTop),
            Size = new Size(396, 240),
        };

        // 2x2 TableLayoutPanel: columns = Left/Right, rows = Top/Bottom
        var cornerTable = new TableLayoutPanel
        {
            ColumnCount = 2,
            RowCount = 2,
            Location = new Point(8, 20),
            Size = new Size(378, 210),
            CellBorderStyle = TableLayoutPanelCellBorderStyle.Single,
        };
        cornerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        cornerTable.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        cornerTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));
        cornerTable.RowStyles.Add(new RowStyle(SizeType.Percent, 50F));

        // Grid positions: [col, row] maps to HotCorner
        //   [0,0] = TopLeft     [1,0] = TopRight
        //   [0,1] = BottomLeft  [1,1] = BottomRight
        var cornerLayout = new[]
        {
            (Corner: HotCorner.TopLeft,     Col: 0, Row: 0, Title: "Top Left"),
            (Corner: HotCorner.TopRight,    Col: 1, Row: 0, Title: "Top Right"),
            (Corner: HotCorner.BottomLeft,  Col: 0, Row: 1, Title: "Bottom Left"),
            (Corner: HotCorner.BottomRight, Col: 1, Row: 1, Title: "Bottom Right"),
        };

        foreach (var (corner, col, row, title) in cornerLayout)
        {
            var cell = BuildCornerCell(corner, title);
            cornerTable.Controls.Add(cell, col, row);
        }

        cornerGroup.Controls.Add(cornerTable);

        // Win key advisory label below the grid
        var winKeyNote = new Label
        {
            Text = "Win key shortcuts (Win+Tab, Win+D, etc.) use the built-in actions above.",
            Location = new Point(12, cornerGroupTop + 248),
            Size = new Size(396, 20),
            Font = new Font(SystemFonts.DefaultFont.FontFamily,
                SystemFonts.DefaultFont.SizeInPoints - 0.5f,
                FontStyle.Italic),
            ForeColor = SystemColors.GrayText,
        };

        // ── Detection group ───────────────────────────────────────────────
        int detectionGroupTop = cornerGroupTop + 276;

        var detectionGroup = new GroupBox
        {
            Text = "Detection",
            Location = new Point(12, detectionGroupTop),
            Size = new Size(396, 82),
        };

        var zoneLabel = MakeLabel("Zone size:", 12, 24);
        _zoneSizeInput = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 50,
            Increment = 1,
            Value = Math.Clamp(settings.ZoneSize, 1, 50),
            Location = new Point(130, 21),
            Width = 65,
        };
        var zonePxLabel = MakeLabel("px", 203, 24);

        var dwellLabel = MakeLabel("Dwell delay:", 12, 54);
        _dwellDelayInput = new NumericUpDown
        {
            Minimum = 50,
            Maximum = 2000,
            Increment = 50,
            Value = Math.Clamp(settings.DwellDelayMs, 50, 2000),
            Location = new Point(130, 51),
            Width = 65,
        };
        var dwellMsLabel = MakeLabel("ms", 203, 54);

        detectionGroup.Controls.AddRange(
        [
            zoneLabel, _zoneSizeInput, zonePxLabel,
            dwellLabel, _dwellDelayInput, dwellMsLabel,
        ]);

        // ── System group ──────────────────────────────────────────────────
        int systemGroupTop = detectionGroupTop + 90;

        var systemGroup = new GroupBox
        {
            Text = "System",
            Location = new Point(12, systemGroupTop),
            Size = new Size(396, 48),
        };

        _startupCheckBox = new CheckBox
        {
            Text = "Start with Windows",
            Location = new Point(12, 18),
            AutoSize = true,
            // Read live from registry (not settings.StartWithWindows) to reflect actual state
            Checked = StartupManager.IsEnabled,
        };

        systemGroup.Controls.Add(_startupCheckBox);

        // ── Window Dragging group ─────────────────────────────────────────────
        int windowDragGroupTop = systemGroupTop + 56;

        var windowDragGroup = new GroupBox
        {
            Text = "Window Dragging",
            Location = new Point(12, windowDragGroupTop),
            Size = new Size(396, 48),
        };

        _windowDragPassThroughCheckBox = new CheckBox
        {
            Text = "Pass through clicks when no window is draggable",
            Location = new Point(12, 18),
            AutoSize = true,
            Checked = settings.WindowDragPassThrough,
        };

        windowDragGroup.Controls.Add(_windowDragPassThroughCheckBox);

        // ── Buttons ───────────────────────────────────────────────────────
        int buttonPanelTop = windowDragGroupTop + 56;

        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Location = new Point(12, buttonPanelTop),
            Size = new Size(396, 32),
            WrapContents = false,
        };

        _cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Width = 80,
            Height = 28,
        };

        _saveButton = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            Width = 80,
            Height = 28,
        };
        _saveButton.Click += OnSaveClick;

        buttonPanel.Controls.AddRange([_cancelButton, _saveButton]);

        AcceptButton = _saveButton;
        CancelButton = _cancelButton;

        // ── Assemble form ─────────────────────────────────────────────────
        // Adjust ClientSize based on actual layout
        ClientSize = new Size(420, buttonPanelTop + 44);

        Controls.AddRange([
            _monitorGroup,
            cornerGroup,
            winKeyNote,
            detectionGroup,
            systemGroup,
            windowDragGroup,
            buttonPanel,
        ]);

        // ── Load first monitor's data into the grid ───────────────────────
        LoadMonitorIntoGrid(_selectedDeviceName);

        ResumeLayout(false);
        PerformLayout();
    }

    // ── Corner cell builder ───────────────────────────────────────────────

    private GroupBox BuildCornerCell(HotCorner corner, string title)
    {
        int idx = (int)corner;

        var box = new GroupBox
        {
            Text = title,
            Dock = DockStyle.Fill,
            Padding = new Padding(6, 4, 6, 4),
        };

        // Action row
        var actionLabel = MakeLabel("Action:", 6, 22);

        var actionCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(62, 19),
            Width = 110,
        };
        foreach (var (label, _) in ActionItems)
            actionCombo.Items.Add(label);
        actionCombo.SelectedIndex = 0;

        _actionCombos[idx] = actionCombo;

        // Shortcut row
        var shortcutLabel = MakeLabel("Shortcut:", 6, 54);

        var recorderPanel = new KeyRecorderPanel
        {
            Location = new Point(62, 50),
            Width = 70,
            Visible = false,
        };
        _recorderPanels[idx] = recorderPanel;

        var recordButton = new Button
        {
            Text = "Record",
            Location = new Point(138, 48),
            Width = 50,
            Height = 24,
            Visible = false,
        };
        _recordButtons[idx] = recordButton;

        // Wire action ComboBox: show/hide recorder and button when Custom selected
        actionCombo.SelectedIndexChanged += (_, _) =>
        {
            bool isCustom = SelectedAction(actionCombo) == CornerAction.Custom;
            recorderPanel.Visible = isCustom;
            recordButton.Visible = isCustom;
        };

        // Wire Record button
        recordButton.Click += (_, _) =>
        {
            _anyPanelRecording = true;
            recorderPanel.StartRecording();
        };

        // Wire ShortcutRecorded
        recorderPanel.ShortcutRecorded += (vks, displayText) =>
        {
            _anyPanelRecording = false;
            // Ensure Custom action is selected
            actionCombo.SelectedIndex = IndexOfAction(CornerAction.Custom);
            recorderPanel.IdleText = displayText;
            recorderPanel.Invalidate();
            UpdatePendingCustomShortcut(corner, vks, displayText);
        };

        // Wire RecordingCancelled
        recorderPanel.RecordingCancelled += () =>
        {
            _anyPanelRecording = false;
            recorderPanel.Invalidate();
        };

        box.Controls.AddRange([actionLabel, actionCombo, shortcutLabel, recorderPanel, recordButton]);
        return box;
    }

    // ── ProcessCmdKey: suppress Escape→CancelButton when recording (Pitfall 2) ──

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (_anyPanelRecording && keyData == Keys.Escape)
            return false; // let Escape reach the focused KeyRecorderPanel
        return base.ProcessCmdKey(ref msg, keyData);
    }

    // ── Monitor switching ─────────────────────────────────────────────────

    private void OnMonitorChanged(object? sender, EventArgs e)
    {
        if (_monitorCombo.SelectedIndex < 0) return;

        // Save current grid state back to pending before switching
        SaveGridToConfig(_selectedDeviceName);

        _selectedDeviceName = _screens[_monitorCombo.SelectedIndex].DeviceName;
        LoadMonitorIntoGrid(_selectedDeviceName);
    }

    private void LoadMonitorIntoGrid(string deviceName)
    {
        if (!_pendingMonitorConfigs.TryGetValue(deviceName, out var config))
            return;

        foreach (HotCorner corner in Enum.GetValues<HotCorner>())
        {
            int idx = (int)corner;

            // Load action
            var action = config.CornerActions.TryGetValue(corner, out var a) ? a : CornerAction.Disabled;
            _actionCombos[idx].SelectedIndex = IndexOfAction(action);

            // Load custom shortcut display text
            if (config.CustomShortcuts.TryGetValue(corner, out var shortcut))
                _recorderPanels[idx].IdleText = shortcut.DisplayText;
            else
                _recorderPanels[idx].IdleText = "(none)";

            _recorderPanels[idx].Invalidate();

            // Show/hide recorder and record button
            bool isCustom = action == CornerAction.Custom;
            _recorderPanels[idx].Visible = isCustom;
            _recordButtons[idx].Visible = isCustom;
        }
    }

    private void SaveGridToConfig(string deviceName)
    {
        if (!_pendingMonitorConfigs.TryGetValue(deviceName, out var config))
            return;

        foreach (HotCorner corner in Enum.GetValues<HotCorner>())
        {
            int idx = (int)corner;
            var action = SelectedAction(_actionCombos[idx]);
            config.CornerActions[corner] = action;
            // Custom shortcut data already updated inline via UpdatePendingCustomShortcut
        }
    }

    private void OnSaveClick(object? sender, EventArgs e)
    {
        // Flush current grid to pending configs before the form closes
        SaveGridToConfig(_selectedDeviceName);
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static CornerAction SelectedAction(ComboBox combo)
    {
        int idx = combo.SelectedIndex;
        if (idx < 0 || idx >= ActionItems.Length) return CornerAction.Disabled;
        return ActionItems[idx].Action;
    }

    private static int IndexOfAction(CornerAction action)
    {
        for (int i = 0; i < ActionItems.Length; i++)
            if (ActionItems[i].Action == action) return i;
        return 0;
    }

    private void UpdatePendingCustomShortcut(HotCorner corner, ushort[] vks, string displayText)
    {
        if (_pendingMonitorConfigs.TryGetValue(_selectedDeviceName, out var config))
            config.CustomShortcuts[corner] = new CustomShortcut(vks, displayText);
    }

    private static Label MakeLabel(string text, int x, int y) => new()
    {
        Text = text,
        Location = new Point(x, y + 3), // +3 aligns baseline with adjacent input
        AutoSize = true,
        ForeColor = SystemColors.ControlText,
    };
}
