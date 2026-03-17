// CornerDetector: detects mouse dwell in a screen corner zone and dispatches via ActionDispatcher.
// 3-state machine: Idle -> Dwelling -> Triggered (Cooldown omitted: Triggered prevents
// re-fire until mouse leaves zone, making a separate Cooldown state redundant).
// Uses System.Windows.Forms.Timer (fires on UI thread) -- NOT System.Timers.Timer.
// Phase 2: HotCorner enum moved to Config/AppSettings.cs. Constructor now takes settings params.
// Phase 2 (Plan 04): OnDwellComplete calls ActionDispatcher.Dispatch() -- not ActionTrigger directly.
// Phase 3: Constructor now takes Rectangle screenBounds + CornerAction; ConfigManager dependency
// removed. Zone check scoped to _screenBounds only (not Screen.AllScreens). UpdateSettings()
// removed -- CornerRouter rebuilds the detector pool on changes.

using System.Drawing;
using System.Windows.Forms;
using WindowsHotSpot.Config;

namespace WindowsHotSpot.Core;

internal enum DetectorState { Idle, Dwelling, Triggered }

internal sealed class CornerDetector : IDisposable
{
    private readonly HotCorner _activeCorner;
    private readonly Rectangle _screenBounds;
    private readonly int _zoneSize;
    private readonly int _dwellDelay;
    private readonly CornerAction _action;

    private DetectorState _state = DetectorState.Idle;
    private bool _isButtonDown;

    // System.Windows.Forms.Timer fires on UI thread -- safe for SendInput and WinForms.
    // Do NOT use System.Timers.Timer or System.Threading.Timer (fire on threadpool).
    private readonly System.Windows.Forms.Timer _dwellTimer;

    public CornerDetector(HotCorner corner, Rectangle screenBounds, int zoneSize, int dwellDelay, CornerAction action)
    {
        _activeCorner = corner;
        _screenBounds = screenBounds;
        _zoneSize = zoneSize;
        _dwellDelay = dwellDelay;
        _action = action;

        _dwellTimer = new System.Windows.Forms.Timer { Interval = _dwellDelay };
        _dwellTimer.Tick += OnDwellComplete;
    }

    /// <summary>
    /// Called from HookManager.MouseMoved event (on UI thread via message loop).
    /// Must be fast -- do NOT call ActionTrigger here; only start/stop the timer.
    /// </summary>
    public void OnMouseMoved(Point pt)
    {
        bool inZone = IsInCornerZone(pt);

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
    // This is where Dispatch() is safe to call -- NOT in OnMouseMoved.
    private void OnDwellComplete(object? sender, EventArgs e)
    {
        _dwellTimer.Stop();
        ActionDispatcher.Dispatch(_action);   // _action fixed at construction; no ConfigManager lookup
        _state = DetectorState.Triggered;
    }

    /// <summary>
    /// Checks if pt is within _zoneSize pixels of the active corner on this detector's screen only.
    /// With Per-Monitor V2 manifest, _screenBounds and hook pt are both in unvirtualized
    /// physical pixels and agree. (CORE-02)
    /// </summary>
    private bool IsInCornerZone(Point pt)
    {
        Point corner = GetCornerPoint(_screenBounds, _activeCorner);
        return Math.Abs(pt.X - corner.X) <= _zoneSize
            && Math.Abs(pt.Y - corner.Y) <= _zoneSize;
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
