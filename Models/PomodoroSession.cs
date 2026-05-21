namespace PomodoroApp.Models;

public class PomodoroSession
{
    public int Id { get; set; }

    /// <summary>What the user was studying during this session.</summary>
    public string Topic { get; set; } = string.Empty;

    public string Mood { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    /// <summary>Configured pomodoro length in minutes (default 25).</summary>
    public int DurationMinutes { get; set; } = 25;

    /// <summary>True when the timer ran to zero; false when the user reset early.</summary>
    public bool Completed { get; set; }
}
