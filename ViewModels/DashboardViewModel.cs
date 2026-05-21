using System.Collections.ObjectModel;
using System.Windows.Media;
using PomodoroApp.Models;
using PomodoroApp.Services;

namespace PomodoroApp.ViewModels;

// ---------------------------------------------------------------------------
// Per-day data for one bar in the chart
// ---------------------------------------------------------------------------

public sealed class DailyBarData
{
    public DateTime Date         { get; init; }
    public int      SessionCount { get; init; }
    public int      TotalMinutes { get; init; }
    public double   BarHeight    { get; init; }  // pixels (0–160)

    public string ValueLabel => TotalMinutes > 0 ? $"{TotalMinutes}m" : "";

    public string DateLabel => Date.Date == DateTime.Today
        ? "Today"
        : Date.ToString("MMM d");

    // Today = bright blue; past days with data = teal; empty = surface colour
    public SolidColorBrush BarBrush => Date.Date == DateTime.Today
        ? new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA))
        : TotalMinutes > 0
            ? new SolidColorBrush(Color.FromRgb(0x74, 0xC7, 0xEC))
            : new SolidColorBrush(Color.FromRgb(0x31, 0x32, 0x44));
}

// ---------------------------------------------------------------------------
// Display wrapper around a saved PomodoroSession
// ---------------------------------------------------------------------------

public sealed class SessionViewModel
{
    private static readonly Dictionary<string, SolidColorBrush> MoodBrushes = new()
    {
        ["Happy"]   = new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1)),
        ["Focused"] = new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA)),
        ["Tired"]   = new SolidColorBrush(Color.FromRgb(0xFA, 0xB3, 0x87)),
        ["Stressed"]= new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8)),
        ["Neutral"] = new SolidColorBrush(Color.FromRgb(0xA6, 0xAD, 0xC8)),
    };

    public SessionViewModel(PomodoroSession s)
    {
        Topic       = s.Topic;
        Mood        = s.Mood;
        Notes       = s.Notes;
        DateDisplay = s.StartTime.ToString("MMM dd, yyyy");
        TimeDisplay = s.StartTime.ToString("hh:mm tt");
        Duration    = $"{s.DurationMinutes} min";
        HasNotes    = !string.IsNullOrWhiteSpace(s.Notes);

        MoodBrush    = MoodBrushes.TryGetValue(s.Mood, out var mb) ? mb : MoodBrushes["Neutral"];
        StatusBrush  = s.Completed
                         ? new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1))
                         : new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8));
        StatusLabel  = s.Completed ? "Complete" : "Abandoned";
    }

    public string         Topic       { get; }
    public string         Mood        { get; }
    public string         Notes       { get; }
    public string         DateDisplay { get; }
    public string         TimeDisplay { get; }
    public string         Duration    { get; }
    public bool           HasNotes    { get; }
    public SolidColorBrush MoodBrush  { get; }
    public SolidColorBrush StatusBrush{ get; }
    public string         StatusLabel { get; }
}

// ---------------------------------------------------------------------------
// Dashboard ViewModel
// ---------------------------------------------------------------------------

public sealed class DashboardViewModel
{
    public ObservableCollection<DailyBarData>    BarData  { get; } = new();
    public ObservableCollection<SessionViewModel> Sessions { get; } = new();

    // Summary stats shown in the header cards
    public int    TotalSessions  { get; private set; }
    public string TotalStudyTime { get; private set; } = "0h 0m";
    public string CompletionRate { get; private set; } = "—";
    public int    WeeklySessions { get; private set; }

    public DashboardViewModel()
    {
        Load(new DataService());
    }

    private void Load(DataService db)
    {
        var all = db.GetAllSessions();

        // ── Header stats ────────────────────────────────────────────────
        TotalSessions = all.Count;

        var completed  = all.Count(s => s.Completed);
        var totalMins  = all.Where(s => s.Completed).Sum(s => s.DurationMinutes);
        TotalStudyTime = $"{totalMins / 60}h {totalMins % 60}m";

        CompletionRate = TotalSessions > 0
            ? $"{(int)(completed * 100.0 / TotalSessions)}%"
            : "—";

        WeeklySessions = all.Count(s => s.StartTime >= DateTime.Today.AddDays(-7));

        // ── Session history list (newest first) ─────────────────────────
        foreach (var s in all)
            Sessions.Add(new SessionViewModel(s));

        // ── 14-day bar chart ────────────────────────────────────────────
        var today  = DateTime.Today;
        int maxMin = 0;

        var daily = Enumerable.Range(0, 14)
            .Select(i => today.AddDays(-(13 - i)))   // oldest → newest (left → right)
            .Select(date =>
            {
                var mins = all
                    .Where(s => s.StartTime.Date == date && s.Completed)
                    .Sum(s => s.DurationMinutes);
                if (mins > maxMin) maxMin = mins;
                return (date, mins, count: all.Count(s => s.StartTime.Date == date));
            })
            .ToList();

        const double MaxPx = 160.0;
        foreach (var (date, mins, count) in daily)
        {
            BarData.Add(new DailyBarData
            {
                Date         = date,
                SessionCount = count,
                TotalMinutes = mins,
                // Ensure non-zero sessions always get at least a 4px sliver
                BarHeight    = maxMin > 0
                    ? Math.Max(mins / (double)maxMin * MaxPx, mins > 0 ? 4 : 0)
                    : 0,
            });
        }
    }
}
