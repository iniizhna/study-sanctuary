using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using PomodoroApp.Models;
using PomodoroApp.Services;

namespace PomodoroApp.ViewModels;

// ---------------------------------------------------------------------------
// Minimal RelayCommand — avoids pulling in a full MVVM framework
// ---------------------------------------------------------------------------

public sealed class RelayCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    public RelayCommand(Action execute, Func<bool>? canExecute = null)
    {
        _execute    = execute;
        _canExecute = canExecute;
    }

    // Piggybacks on WPF's own CommandManager so CanExecute is re-evaluated
    // automatically whenever any UI input event fires.
    public event EventHandler? CanExecuteChanged
    {
        add    => CommandManager.RequerySuggested += value;
        remove => CommandManager.RequerySuggested -= value;
    }

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
    public void Execute(object? parameter)    => _execute();
}

// ---------------------------------------------------------------------------
// ViewModel
// ---------------------------------------------------------------------------

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly TimerService _timer;
    private readonly DataService  _db;

    // ------------------------------------------------------------------
    // Backing fields
    // ------------------------------------------------------------------

    private string _timeDisplay   = "25:00";
    private string _statusMessage = "Ready to focus!";
    private string _topic         = string.Empty;
    private string _selectedMood  = "Neutral";
    private string _notes         = string.Empty;
    private bool   _isRunning;
    private bool   _isWorkMode    = true;
    private bool   _isFullScreen;

    // ------------------------------------------------------------------
    // Bound properties
    // ------------------------------------------------------------------

    public string TimeDisplay
    {
        get => _timeDisplay;
        private set => Set(ref _timeDisplay, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => Set(ref _statusMessage, value);
    }

    public string Topic
    {
        get => _topic;
        set => Set(ref _topic, value);
    }

    public string SelectedMood
    {
        get => _selectedMood;
        set => Set(ref _selectedMood, value);
    }

    public string Notes
    {
        get => _notes;
        set => Set(ref _notes, value);
    }

    public bool IsRunning
    {
        get => _isRunning;
        private set
        {
            if (Set(ref _isRunning, value))
                CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool IsWorkMode
    {
        get => _isWorkMode;
        private set
        {
            if (!Set(ref _isWorkMode, value)) return;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRestMode)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TimerForeground)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModeLabel)));
        }
    }

    public bool IsRestMode => !_isWorkMode;

    /// <summary>Blue for work, green for rest — drives the large countdown colour.</summary>
    public SolidColorBrush TimerForeground => _isWorkMode
        ? new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA))
        : new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1));

    /// <summary>Label shown in the full-screen overlay.</summary>
    public string ModeLabel => _isWorkMode ? "⏱  WORK SESSION" : "☕  REST BREAK";

    public bool IsFullScreen
    {
        get => _isFullScreen;
        private set
        {
            if (!Set(ref _isFullScreen, value)) return;
            FullScreenToggled?.Invoke(value);
        }
    }

    /// <summary>Fires when full-screen state changes so code-behind can update WindowStyle/State.</summary>
    public event Action<bool>? FullScreenToggled;

    /// <summary>Mood options shown in the ComboBox.</summary>
    public IReadOnlyList<string> Moods { get; } =
        new[] { "Happy", "Focused", "Tired", "Stressed", "Neutral" };

    // ------------------------------------------------------------------
    // Commands
    // ------------------------------------------------------------------

    public ICommand StartCommand         { get; }
    public ICommand PauseCommand         { get; }
    public ICommand ResetCommand         { get; }
    public ICommand SetWorkModeCommand      { get; }
    public ICommand SetRestModeCommand      { get; }
    public ICommand ToggleFullScreenCommand { get; }
    public ICommand ExitFullScreenCommand   { get; }
    public ICommand OpenDashboardCommand    { get; }

    // ------------------------------------------------------------------
    // Constructor
    // ------------------------------------------------------------------

    public MainWindowViewModel()
    {
        _timer = new TimerService(durationMinutes: 25);
        _db    = new DataService();

        _timer.Tick      += remaining => TimeDisplay = FormatTime(remaining);
        _timer.Completed += OnTimerCompleted;

        StartCommand         = new RelayCommand(Start, () => !IsRunning);
        PauseCommand         = new RelayCommand(Pause, () => IsRunning);
        ResetCommand         = new RelayCommand(Reset);
        SetWorkModeCommand      = new RelayCommand(SetWorkMode);
        SetRestModeCommand      = new RelayCommand(SetRestMode);
        ToggleFullScreenCommand = new RelayCommand(() => IsFullScreen = !IsFullScreen);
        ExitFullScreenCommand   = new RelayCommand(() => IsFullScreen = false);
        OpenDashboardCommand    = new RelayCommand(OpenDashboard);
    }

    // ------------------------------------------------------------------
    // Command implementations
    // ------------------------------------------------------------------

    private void Start()
    {
        if (_isWorkMode && string.IsNullOrWhiteSpace(Topic))
        {
            StatusMessage = "Please enter a study topic before starting.";
            return;
        }

        _timer.Start();
        IsRunning     = true;
        StatusMessage = _isWorkMode ? $"Focusing on: {Topic}" : "Rest time — you've earned it!";
    }

    private void Pause()
    {
        _timer.Pause();
        IsRunning     = false;
        StatusMessage = _isWorkMode ? "Paused — ready to continue?" : "Break paused.";
    }

    private void Reset()
    {
        _timer.Reset(_isWorkMode ? 25 : 5);
        IsRunning     = false;
        StatusMessage = _isWorkMode ? "Timer reset. Ready to start!" : "Break reset.";
    }

    private void SetWorkMode()
    {
        if (_isWorkMode) return;
        IsWorkMode    = true;
        _timer.Reset(25);
        StatusMessage = "Ready to focus!";
    }

    private void SetRestMode()
    {
        if (!_isWorkMode) return;
        IsWorkMode    = false;
        _timer.Reset(5);
        StatusMessage = "Take a well-earned break!";
    }

    private static void OpenDashboard()
    {
        // Opens non-modal so the timer keeps running while the user browses history.
        var dashboard = new PomodoroApp.DashboardWindow();
        dashboard.Show();
    }

    // ------------------------------------------------------------------
    // Timer completion
    // ------------------------------------------------------------------

    private void OnTimerCompleted()
    {
        IsRunning = false;

        if (_isWorkMode)
        {
            StatusMessage = "Session complete — great work!";
            PersistSession(completed: true);
            MessageBox.Show(
                $"Pomodoro complete!\n\nTopic: {Topic}\nMood: {SelectedMood}\n\nTime for a break!",
                "Session Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        else
        {
            StatusMessage = "Break's over — back to it!";
            MessageBox.Show(
                "Break complete!\n\nReady to start your next Pomodoro?",
                "Break Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }

    // ------------------------------------------------------------------
    // Persistence
    // ------------------------------------------------------------------

    private void PersistSession(bool completed)
    {
        var session = new PomodoroSession
        {
            Topic           = string.IsNullOrWhiteSpace(Topic) ? "Untitled" : Topic.Trim(),
            Mood            = SelectedMood,
            Notes           = Notes.Trim(),
            StartTime       = _timer.SessionStartTime,
            EndTime         = DateTime.Now,
            DurationMinutes = 25,
            Completed       = completed,
        };

        // Fire-and-forget on a thread pool thread so the UI doesn't stutter
        // if the disk is slow; SQLite writes are fast in practice but this
        // keeps the completion MessageBox from being delayed.
        Task.Run(() => _db.SaveSession(session));
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    private static string FormatTime(TimeSpan t)
        => $"{(int)t.TotalMinutes:D2}:{t.Seconds:D2}";

    // ------------------------------------------------------------------
    // INotifyPropertyChanged
    // ------------------------------------------------------------------

    public event PropertyChangedEventHandler? PropertyChanged;

    // Returns true when the value actually changed — used by IsRunning setter.
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}
