// KeyRecorderPanel: focusable Panel subclass for keystroke recording.
// Uses PreviewKeyDown to mark all keys as input keys so KeyDown receives Tab, Escape, arrows, Enter.
// Win key (LWin/RWin) captured via WH_KEYBOARD_LL hook installed during recording.
// Single-key alphanumeric shortcuts are rejected with a validation message (research Open Question 1).

using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using WindowsHotSpot.Native;

namespace WindowsHotSpot.UI;

internal sealed class KeyRecorderPanel : Panel
{
    // Fired when user presses a valid shortcut. Provides VK sequence and display text.
    // VKs: modifier VKs first (VK_LCONTROL/VK_LSHIFT/VK_LMENU), then main key VK.
    public event Action<ushort[], string>? ShortcutRecorded;

    // Fired when user presses Escape alone (no modifiers) — cancel without saving.
    public event Action? RecordingCancelled;

    private bool _isRecording;

    // Keyboard hook for Win key capture (WH_KEYBOARD_LL)
    private IntPtr _keyboardHook = IntPtr.Zero;
    private NativeMethods.LowLevelKeyboardProc? _keyboardHookProc; // pinned — prevent GC during hook lifetime
    private bool _winKeyDown;
    private ushort _winVk; // VK_LWIN or VK_RWIN captured by hook

    // Text to show when idle (not recording). Caller sets this to the current shortcut label.
    [DefaultValue("(none)")]
    public string IdleText { get; set; } = "(none)";

    public KeyRecorderPanel()
    {
        SetStyle(ControlStyles.Selectable, true);
        TabStop = true;
        // Visual styling: look like a read-only text field
        BorderStyle = BorderStyle.Fixed3D;
        BackColor = SystemColors.Window;
        Height = 24;
        Padding = new Padding(4, 3, 4, 3);
    }

    /// <summary>
    /// Enters recording mode: focuses the panel and changes display text to prompt.
    /// </summary>
    public void StartRecording()
    {
        InstallKeyboardHook();
        _isRecording = true;
        Invalidate(); // trigger repaint with "Press a key..." prompt
        Focus();
    }

    /// <summary>
    /// Cancels recording without firing any event. Used when parent form closes.
    /// </summary>
    public void CancelRecording()
    {
        UninstallKeyboardHook();
        _isRecording = false;
        Invalidate();
    }

    public bool IsRecording => _isRecording;

    protected override void Dispose(bool disposing)
    {
        if (disposing) UninstallKeyboardHook();
        base.Dispose(disposing);
    }

    private void InstallKeyboardHook()
    {
        _keyboardHookProc = KeyboardHookCallback;
        using var process = System.Diagnostics.Process.GetCurrentProcess();
        using var module = process.MainModule!;
        var hMod = NativeMethods.GetModuleHandle(module.ModuleName!);
        _keyboardHook = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _keyboardHookProc, hMod, 0);
    }

    private void UninstallKeyboardHook()
    {
        if (_keyboardHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_keyboardHook);
            _keyboardHook = IntPtr.Zero;
            _keyboardHookProc = null;
        }
        _winKeyDown = false;
        _winVk = 0;
    }

    private IntPtr KeyboardHookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && _isRecording)
        {
            var kb = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            bool isDown = wParam == (IntPtr)NativeMethods.WM_KEYDOWN || wParam == (IntPtr)NativeMethods.WM_SYSKEYDOWN;
            bool isUp   = wParam == (IntPtr)NativeMethods.WM_KEYUP   || wParam == (IntPtr)NativeMethods.WM_SYSKEYUP;

            if (kb.vkCode == NativeMethods.VK_LWIN || kb.vkCode == NativeMethods.VK_RWIN)
            {
                if (isDown) { _winKeyDown = true; _winVk = (ushort)kb.vkCode; }
                if (isUp)   { _winKeyDown = false; }
                return (IntPtr)1; // Block Win key from reaching Windows shell during recording
            }
        }
        return NativeMethods.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
    }

    protected override void OnPreviewKeyDown(PreviewKeyDownEventArgs e)
    {
        // Force all keys to be treated as input keys so KeyDown fires for Tab (0x09),
        // Escape (0x1B), arrow keys, Enter — WinForms normally routes these as dialog keys.
        if (_isRecording)
            e.IsInputKey = true;
        base.OnPreviewKeyDown(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!_isRecording)
        {
            base.OnKeyDown(e);
            return;
        }

        e.Handled = true;
        e.SuppressKeyPress = true;

        // Escape alone = cancel
        if (e.KeyCode == Keys.Escape && e.Modifiers == Keys.None)
        {
            UninstallKeyboardHook();
            _isRecording = false;
            Invalidate();
            RecordingCancelled?.Invoke();
            return;
        }

        // Modifier-only keypress: keep waiting for the main key
        if (IsModifierOnly(e.KeyCode))
            return;

        // Reject bare alphanumeric keys (letters A-Z, digits 0-9) without any modifier.
        // Win key counts as a modifier here (_winKeyDown tracked via WH_KEYBOARD_LL).
        if (e.Modifiers == Keys.None && !_winKeyDown && IsBareAlphanumeric(e.KeyCode))
        {
            // Flash a validation hint — use the panel text; caller repaints after RecordingCancelled
            // is not raised here. Instead, keep recording but show a warning in the display.
            // Simplest approach: fire a brief repaint with advisory text, then stay in recording mode.
            _validationMessage = "Add Ctrl, Shift, or Alt for letter/number keys";
            Invalidate();
            return; // keep _isRecording = true; user can try again
        }

        _validationMessage = null;

        // Build VK sequence: Win key first (if held), then left-side modifier VKs, then main key VK
        ushort[] vks = BuildVkSequence(e);

        // Build display text: use KeysConverter on the full Keys value (modifier flags + key code)
        string displayText = BuildDisplayText(e);

        UninstallKeyboardHook();
        _isRecording = false;
        Invalidate();
        ShortcutRecorded?.Invoke(vks, displayText);
    }

    private string? _validationMessage;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        string text = _isRecording
            ? (_validationMessage ?? "Press a key...")
            : IdleText;
        Color color = _validationMessage != null
            ? Color.Firebrick
            : (_isRecording ? SystemColors.HotTrack : SystemColors.ControlText);
        using var brush = new SolidBrush(color);
        var format = new StringFormat { Alignment = StringAlignment.Near, LineAlignment = StringAlignment.Center };
        e.Graphics.DrawString(text, Font, brush, new RectangleF(4, 0, Width - 8, Height), format);
    }

    private static bool IsModifierOnly(Keys key) => key is
        Keys.ShiftKey or Keys.LShiftKey or Keys.RShiftKey or
        Keys.ControlKey or Keys.LControlKey or Keys.RControlKey or
        Keys.Menu or Keys.LMenu or Keys.RMenu;

    private static bool IsBareAlphanumeric(Keys key) =>
        (key >= Keys.A && key <= Keys.Z) ||
        (key >= Keys.D0 && key <= Keys.D9);

    private ushort[] BuildVkSequence(KeyEventArgs e)
    {
        // Use left-side specific VK codes for modifiers (VK_LCONTROL 0xA2, VK_LSHIFT 0xA0,
        // VK_LMENU 0xA4) — consistent with ActionDispatcher.SendWinKey pattern.
        var vks = new List<ushort>();
        if (_winKeyDown)                        vks.Add(_winVk);  // Win key first (before Ctrl/Shift/Alt)
        if ((e.Modifiers & Keys.Control) != 0) vks.Add(0xA2);    // VK_LCONTROL
        if ((e.Modifiers & Keys.Shift)   != 0) vks.Add(0xA0);    // VK_LSHIFT
        if ((e.Modifiers & Keys.Alt)     != 0) vks.Add(0xA4);    // VK_LMENU
        vks.Add((ushort)e.KeyCode);
        return [.. vks];
    }

    private string BuildDisplayText(KeyEventArgs e)
    {
        // Build a combined Keys value (modifier flags OR'd with the key code).
        // KeysConverter produces human-readable labels: "Ctrl+F5", "Alt+Home", "Shift+F1".
        var combined = e.Modifiers | e.KeyCode;
        var converter = new KeysConverter();
        string text = converter.ConvertToString(combined) ?? combined.ToString();
        if (_winKeyDown)
            text = "Win+" + text;
        return text;
    }
}
