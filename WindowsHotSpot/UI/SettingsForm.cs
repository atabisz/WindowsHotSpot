// SettingsForm: modal settings dialog for WindowsHotSpot.
// Manual layout (no designer file) for diffability.
// AutoScaleMode.Dpi prevents blurry controls on high-DPI displays (research Pitfall 5).
// ShowDialog() is safe from the hook thread: it runs its own nested message loop that
// still dispatches WH_MOUSE_LL callbacks (research Pitfall 4).

using System.Drawing;
using System.Windows.Forms;
using WindowsHotSpot.Config;

namespace WindowsHotSpot.UI;

internal sealed class SettingsForm : Form
{
    private readonly ComboBox _cornerCombo;
    private readonly NumericUpDown _zoneSizeInput;
    private readonly NumericUpDown _dwellDelayInput;
    private readonly CheckBox _startupCheckBox;
    private readonly Button _saveButton;
    private readonly Button _cancelButton;

    // Maps display text → enum value for the corner ComboBox
    private static readonly (string Label, HotCorner Value)[] CornerItems =
    [
        ("Top Left",     HotCorner.TopLeft),
        ("Top Right",    HotCorner.TopRight),
        ("Bottom Left",  HotCorner.BottomLeft),
        ("Bottom Right", HotCorner.BottomRight),
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
        ClientSize = new Size(360, 290);
        Padding = new Padding(0);
        BackColor = SystemColors.Window;

        // ── Detection group ──────────────────────────────────────────────
        var detectionGroup = new GroupBox
        {
            Text = "Detection",
            Location = new Point(12, 12),
            Size = new Size(336, 118),
            ForeColor = SystemColors.ControlText,
        };

        var cornerLabel = MakeLabel("Active corner:", 12, 24);
        _cornerCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(130, 21),
            Width = 140,
        };
        foreach (var (label, _) in CornerItems)
            _cornerCombo.Items.Add(label);
        _cornerCombo.SelectedIndex = Array.FindIndex(CornerItems, x => x.Value == settings.Corner);

        var zoneLabel = MakeLabel("Zone size:", 12, 60);
        _zoneSizeInput = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 50,
            Increment = 1,
            Value = settings.ZoneSize,
            Location = new Point(130, 57),
            Width = 65,
        };
        var zonePxLabel = MakeLabel("px", 203, 60);

        var dwellLabel = MakeLabel("Dwell delay:", 12, 96);
        _dwellDelayInput = new NumericUpDown
        {
            Minimum = 50,
            Maximum = 2000,
            Increment = 50,
            Value = settings.DwellDelayMs,
            Location = new Point(130, 93),
            Width = 65,
        };
        var dwellMsLabel = MakeLabel("ms", 203, 96);

        detectionGroup.Controls.AddRange(
        [
            cornerLabel, _cornerCombo,
            zoneLabel, _zoneSizeInput, zonePxLabel,
            dwellLabel, _dwellDelayInput, dwellMsLabel,
        ]);

        // ── System group ─────────────────────────────────────────────────
        var systemGroup = new GroupBox
        {
            Text = "System",
            Location = new Point(12, 140),
            Size = new Size(336, 54),
        };

        _startupCheckBox = new CheckBox
        {
            Text = "Start with Windows",
            Location = new Point(12, 22),
            AutoSize = true,
            // Read live from registry (not settings.StartWithWindows) to reflect actual state
            Checked = StartupManager.IsEnabled,
        };

        systemGroup.Controls.Add(_startupCheckBox);

        // ── Buttons ───────────────────────────────────────────────────────
        var buttonPanel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Location = new Point(12, 207),
            Size = new Size(336, 32),
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

        buttonPanel.Controls.AddRange([_cancelButton, _saveButton]);

        AcceptButton = _saveButton;
        CancelButton = _cancelButton;

        Controls.AddRange([detectionGroup, systemGroup, buttonPanel]);

        ResumeLayout(false);
        PerformLayout();
    }

    public HotCorner SelectedCorner => CornerItems[_cornerCombo.SelectedIndex].Value;
    public int SelectedZoneSize => (int)_zoneSizeInput.Value;
    public int SelectedDwellDelay => (int)_dwellDelayInput.Value;
    public bool SelectedStartWithWindows => _startupCheckBox.Checked;

    private static Label MakeLabel(string text, int x, int y) => new()
    {
        Text = text,
        Location = new Point(x, y + 3), // +3 aligns baseline with adjacent input
        AutoSize = true,
        ForeColor = SystemColors.ControlText,
    };
}
