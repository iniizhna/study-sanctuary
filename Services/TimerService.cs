using System.Windows.Threading;

namespace PomodoroApp.Services;

/// <summary>
/// Encapsulates Pomodoro countdown logic.
/// Uses DispatcherTimer so Tick fires on the UI thread — no cross-thread marshalling needed.
/// </summary>
public class TimerService
{
    private readonly DispatcherTimer _timer;
    private TimeSpan _remaining;
    private DateTime _sessionStartTime;
    private bool _sessionStarted;  // distinguishes fresh-start from resume-after-pause

    // ------------------------------------------------------------------
    // Events
    // ------------------------------------------------------------------

    /// <summary>Fires every second while running, providing the updated remaining time.</summary>
    public event Action<TimeSpan>? Tick;

    /// <summary>Fires exactly once when the countdown reaches zero.</summary>
    public event Action? Completed;

    // ------------------------------------------------------------------
    // State
    // ------------------------------------------------------------------

    public bool IsRunning { get; private set; }

    /// <summary>Current remaining duration (updated each tick).</summary>
    public TimeSpan Remaining => _remaining;

    /// <summary>
    /// Wall-clock time at which the user first pressed Start for this session.
    /// Not updated on resume after pause — represents session start, not last resume.
    /// </summary>
    public DateTime SessionStartTime => _sessionStartTime;

    // ------------------------------------------------------------------
    // Constructor
    // ------------------------------------------------------------------

    public TimerService(int durationMinutes = 25)
    {
        _remaining = TimeSpan.FromMinutes(durationMinutes);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += OnTick;
    }

    // ------------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------------

    /// <summary>Starts the timer. If already running this is a no-op.</summary>
    public void Start()
    {
        if (IsRunning) return;

        // Only capture the session start on the very first Start() call.
        if (!_sessionStarted)
        {
            _sessionStartTime = DateTime.Now;
            _sessionStarted   = true;
        }

        IsRunning = true;
        _timer.Start();
    }

    /// <summary>Pauses the countdown without resetting it.</summary>
    public void Pause()
    {
        _timer.Stop();
        IsRunning = false;
    }

    /// <summary>Resets the countdown to the full duration and clears session state.</summary>
    public void Reset(int durationMinutes = 25)
    {
        _timer.Stop();
        IsRunning      = false;
        _sessionStarted = false;
        _remaining     = TimeSpan.FromMinutes(durationMinutes);

        // Notify so the UI refreshes immediately without waiting for the next tick.
        Tick?.Invoke(_remaining);
    }

    // ------------------------------------------------------------------
    // Private
    // ------------------------------------------------------------------

    private void OnTick(object? sender, EventArgs e)
    {
        _remaining -= TimeSpan.FromSeconds(1);
        Tick?.Invoke(_remaining);

        if (_remaining <= TimeSpan.Zero)
        {
            _timer.Stop();
            IsRunning = false;
            Completed?.Invoke();
        }
    }
}
