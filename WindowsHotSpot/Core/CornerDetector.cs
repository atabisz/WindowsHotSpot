// CornerDetector: detects mouse dwell in a screen corner zone and triggers ActionTrigger.
// 3-state machine: Idle -> Dwelling -> Triggered (Cooldown omitted: Triggered prevents
// re-fire until mouse leaves zone, making a separate Cooldown state redundant).
// Uses System.Windows.Forms.Timer (fires on UI thread) -- NOT System.Timers.Timer.

using System.Drawing;
using System.Windows.Forms;

namespace WindowsHotSpot.Core;

internal enum HotCorner { TopLeft, TopRight, BottomLeft, BottomRight }

internal enum DetectorState { Idle, Dwelling, Triggered }

internal sealed class CornerDetector : IDisposable
{
    // Hard-coded for Phase 1; extracted to config in Phase 2
    private readonly HotCorner _activeCorner = HotCorner.TopLeft;
    private readonly int _zoneSize = 10;   // pixels from corner
    private readonly int _dwellDelay = 300; // milliseconds

    private DetectorState _state = DetectorState.Idle;
    private bool _isButtonDown;

    // System.Windows.Forms.Timer fires on UI thread -- safe for SendInput and WinForms.
    // Do NOT use System.Timers.Timer or System.Threading.Timer (fire on threadpool).
    private readonly System.Windows.Forms.Timer _dwellTimer;

    public CornerDetector()
    {
        _dwellTimer = new System.Windows.Forms.Timer { Interval = _dwellDelay };
        _dwellTimer.Tick += OnDwellComplete;
    }

    /// <summary>
    /// Called from HookManager.MouseMoved event (on UI thread via message loop).
    /// Must be fast -- do NOT call ActionTrigger here; only start/stop the timer.
    /// </summary>
    public void OnMouseMoved(Point pt)
    {
        bool inZone = IsInAnyCornerZone(pt);

        switch (_state)
        {
            case DetectorState.Idle:
                if (inZone && !_isButtonDown)
                {
                    _dwellTimer.Start();
                    _state = DetectorState.Dwelling;
                }
                break;

            case DetectorState.Dwelling:
                if (!inZone)
                {
                    _dwellTimer.Stop();
                    _state = DetectorState.Idle;
                }
                else if (_isButtonDown)
                {
                    // Drag suppression (CORE-04): cancel dwell if button held
                    _dwellTimer.Stop();
                    _state = DetectorState.Idle;
                }
                break;

            case DetectorState.Triggered:
                if (!inZone)
                {
                    // Mouse left zone: re-arm (CORE-05)
                    _state = DetectorState.Idle;
                }
                // Triggered + in zone -> no-op: prevents double-trigger
                break;
        }
    }

    /// <summary>
    /// Called from HookManager.MouseButtonChanged event (on UI thread).
    /// Cancels dwell immediately if button goes down while dwelling (CORE-04).
    /// </summary>
    public void OnMouseButtonChanged(bool isDown)
    {
        _isButtonDown = isDown;
        if (isDown && _state == DetectorState.Dwelling)
        {
            _dwellTimer.Stop();
            _state = DetectorState.Idle;
        }
    }

    // Timer Tick fires on UI thread (System.Windows.Forms.Timer).
    // This is where SendTaskView() is safe to call -- NOT in OnMouseMoved.
    private void OnDwellComplete(object? sender, EventArgs e)
    {
        _dwellTimer.Stop();
        ActionTrigger.SendTaskView();
        _state = DetectorState.Triggered;
    }

    /// <summary>
    /// Checks if pt is within _zoneSize pixels of the active corner on ANY connected screen.
    /// With Per-Monitor V2 manifest, Screen.AllScreens bounds and hook pt are both in
    /// unvirtualized physical pixels and agree. (CORE-02)
    /// </summary>
    private bool IsInAnyCornerZone(Point pt)
    {
        foreach (var screen in Screen.AllScreens)
        {
            Point corner = GetCornerPoint(screen.Bounds, _activeCorner);
            if (Math.Abs(pt.X - corner.X) <= _zoneSize
             && Math.Abs(pt.Y - corner.Y) <= _zoneSize)
            {
                return true;
            }
        }
        return false;
    }

    private static Point GetCornerPoint(Rectangle bounds, HotCorner corner) => corner switch
    {
        HotCorner.TopLeft     => new Point(bounds.Left,     bounds.Top),
        HotCorner.TopRight    => new Point(bounds.Right - 1, bounds.Top),
        HotCorner.BottomLeft  => new Point(bounds.Left,     bounds.Bottom - 1),
        HotCorner.BottomRight => new Point(bounds.Right - 1, bounds.Bottom - 1),
        _ => throw new ArgumentOutOfRangeException(nameof(corner))
    };

    public void Dispose()
    {
        _dwellTimer.Stop();
        _dwellTimer.Dispose();
    }
}
