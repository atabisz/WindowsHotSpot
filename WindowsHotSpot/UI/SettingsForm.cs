// SettingsForm: modal settings dialog for WindowsHotSpot.
// Manual layout (no designer file) for diffability.
// AutoScaleMode.Dpi prevents blurry controls on high-DPI displays (research Pitfall 5).
// ShowDialog() is safe from the hook thread: it runs its own nested message loop that
// still dispatches WH_MOUSE_LL callbacks (research Pitfall 4).

using System.Windows.Forms;
using WindowsHotSpot.Config;

namespace WindowsHotSpot.UI;

internal sealed class SettingsForm : Form
{
    // Controls
    private readonly ComboBox _cornerCombo;
    private readonly NumericUpDown _zoneSizeInput;
    private readonly NumericUpDown _dwellDelayInput;
    private readonly CheckBox _startupCheckBox;
    private readonly Button _saveButton;
    private readonly Button _cancelButton;

    public SettingsForm(AppSettings settings)
    {
        // Form properties
        Text = "WindowsHotSpot Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new System.Drawing.Size(310, 190);

        // --- Corner ---
        var cornerLabel = new Label
        {
            Text = "Active Corner:",
            Location = new System.Drawing.Point(12, 18),
            AutoSize = true
        };

        _cornerCombo = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            DataSource = Enum.GetValues<HotCorner>(),
            Location = new System.Drawing.Point(140, 15),
            Width = 150
        };
        _cornerCombo.SelectedItem = settings.Corner;

        // --- Zone Size ---
        var zoneSizeLabel = new Label
        {
            Text = "Zone Size (px):",
            Location = new System.Drawing.Point(12, 51),
            AutoSize = true
        };

        _zoneSizeInput = new NumericUpDown
        {
            Minimum = 1,
            Maximum = 50,
            Increment = 1,
            Value = settings.ZoneSize,
            Location = new System.Drawing.Point(140, 48),
            Width = 80
        };

        // --- Dwell Delay ---
        var dwellDelayLabel = new Label
        {
            Text = "Dwell Delay (ms):",
            Location = new System.Drawing.Point(12, 84),
            AutoSize = true
        };

        _dwellDelayInput = new NumericUpDown
        {
            Minimum = 50,
            Maximum = 2000,
            Increment = 50,
            Value = settings.DwellDelayMs,
            Location = new System.Drawing.Point(140, 81),
            Width = 80
        };

        // --- Start with Windows ---
        _startupCheckBox = new CheckBox
        {
            Text = "Start with Windows",
            Location = new System.Drawing.Point(12, 117),
            AutoSize = true,
            // Read live from registry (not settings.StartWithWindows) to reflect actual state
            Checked = StartupManager.IsEnabled
        };

        // --- Buttons ---
        _saveButton = new Button
        {
            Text = "Save",
            DialogResult = DialogResult.OK,
            Location = new System.Drawing.Point(140, 153),
            Width = 75
        };

        _cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Location = new System.Drawing.Point(225, 153),
            Width = 75
        };

        AcceptButton = _saveButton;
        CancelButton = _cancelButton;

        Controls.AddRange(new Control[]
        {
            cornerLabel, _cornerCombo,
            zoneSizeLabel, _zoneSizeInput,
            dwellDelayLabel, _dwellDelayInput,
            _startupCheckBox,
            _saveButton, _cancelButton
        });
    }

    // Public read-only properties for caller to read selected values
    public HotCorner SelectedCorner => (HotCorner)_cornerCombo.SelectedItem!;
    public int SelectedZoneSize => (int)_zoneSizeInput.Value;
    public int SelectedDwellDelay => (int)_dwellDelayInput.Value;
    public bool SelectedStartWithWindows => _startupCheckBox.Checked;
}
