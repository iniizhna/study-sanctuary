using System.IO;
using Microsoft.Data.Sqlite;
using PomodoroApp.Models;

namespace PomodoroApp.Services;

/// <summary>
/// Handles all SQLite persistence for completed Pomodoro sessions.
/// The database file is created next to the executable on first run.
/// </summary>
public class DataService
{
    private readonly string _connectionString;

    public DataService(string dbPath = "pomodoro_sessions.db")
    {
        // Resolve relative path against the executable directory so the db
        // file doesn't end up in System32 when run elevated.
        if (!Path.IsPathRooted(dbPath))
            dbPath = Path.Combine(AppContext.BaseDirectory, dbPath);

        _connectionString = $"Data Source={dbPath}";
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var conn = OpenConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS Sessions (
                Id              INTEGER PRIMARY KEY AUTOINCREMENT,
                Topic           TEXT    NOT NULL,
                Mood            TEXT    NOT NULL,
                Notes           TEXT    NOT NULL DEFAULT '',
                StartTime       TEXT    NOT NULL,
                EndTime         TEXT    NOT NULL,
                DurationMinutes INTEGER NOT NULL,
                Completed       INTEGER NOT NULL DEFAULT 0
            )";
        cmd.ExecuteNonQuery();
    }

    /// <summary>Persists a completed (or abandoned) session to the database.</summary>
    public void SaveSession(PomodoroSession session)
    {
        using var conn = OpenConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO Sessions (Topic, Mood, Notes, StartTime, EndTime, DurationMinutes, Completed)
            VALUES ($topic, $mood, $notes, $start, $end, $duration, $completed)";

        cmd.Parameters.AddWithValue("$topic",     session.Topic);
        cmd.Parameters.AddWithValue("$mood",      session.Mood);
        cmd.Parameters.AddWithValue("$notes",     session.Notes);
        // ISO 8601 round-trip format — unambiguous across locales
        cmd.Parameters.AddWithValue("$start",     session.StartTime.ToString("O"));
        cmd.Parameters.AddWithValue("$end",       session.EndTime.ToString("O"));
        cmd.Parameters.AddWithValue("$duration",  session.DurationMinutes);
        cmd.Parameters.AddWithValue("$completed", session.Completed ? 1 : 0);

        cmd.ExecuteNonQuery();
    }

    /// <summary>Returns all sessions ordered newest-first.</summary>
    public List<PomodoroSession> GetAllSessions()
    {
        var sessions = new List<PomodoroSession>();

        using var conn = OpenConnection();
        var cmd = conn.CreateCommand();
        cmd.CommandText =
            "SELECT Id, Topic, Mood, Notes, StartTime, EndTime, DurationMinutes, Completed " +
            "FROM Sessions ORDER BY StartTime DESC";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            sessions.Add(new PomodoroSession
            {
                Id              = reader.GetInt32(0),
                Topic           = reader.GetString(1),
                Mood            = reader.GetString(2),
                Notes           = reader.GetString(3),
                StartTime       = DateTime.Parse(reader.GetString(4)),
                EndTime         = DateTime.Parse(reader.GetString(5)),
                DurationMinutes = reader.GetInt32(6),
                Completed       = reader.GetInt32(7) == 1,
            });
        }

        return sessions;
    }

    private SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }
}
