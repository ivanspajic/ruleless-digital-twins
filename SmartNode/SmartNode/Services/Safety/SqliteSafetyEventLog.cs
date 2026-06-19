using System.Globalization;
using Microsoft.Data.Sqlite;
using SmartNode.Mapek.Execution;

namespace SmartNode.Services.Safety;

// Durable, SQLite-backed safety-event audit log (P1 / criterion 7 — safety
// events persisted; criterion 10 — audit log). Persists every blocked attempt,
// failure, and successful actuation so the audit trail survives a SmartNode
// restart. Thread-safe via a single guarded connection. Stores NO secrets — only
// the gate, outcome, a human-readable detail, optional entity/domain/service, and
// optional scenario/goal ids. Fail-fast: the constructor throws a clear error if
// the database cannot be created/opened, so a misconfigured path is reported at
// startup rather than silently losing the audit trail.
public sealed class SqliteSafetyEventLog : ISafetyEventLog, IDisposable
{
    private readonly int _capacity;
    private readonly object _gate = new();
    private readonly SqliteConnection _connection;
    private bool _disposed;

    public SqliteSafetyEventLog(string databasePath, int capacity = 2000)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Safety event log SQLite path must not be empty.", nameof(databasePath));
        }
        _capacity = capacity > 0 ? capacity : 2000;

        var fullPath = Path.GetFullPath(databasePath);
        try
        {
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var connectionString = new SqliteConnectionStringBuilder { DataSource = fullPath }.ToString();
            _connection = new SqliteConnection(connectionString);
            _connection.Open();

            Execute("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;");
            Execute(@"
CREATE TABLE IF NOT EXISTS safety_events (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp_utc TEXT    NOT NULL,
    outcome       TEXT    NOT NULL,
    gate          TEXT    NOT NULL,
    detail        TEXT    NOT NULL,
    entity_id     TEXT    NULL,
    domain        TEXT    NULL,
    service       TEXT    NULL,
    scenario_id   TEXT    NULL,
    goal_id       TEXT    NULL
);");
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new InvalidOperationException(
                $"Failed to initialise the SQLite safety event log at '{fullPath}'. " +
                $"Check {SafetyEventLogOptions.SqlitePathEnvVar} and filesystem permissions. ({ex.Message})", ex);
        }
    }

    public void Record(SafetyEventRecord safetyEvent)
    {
        ArgumentNullException.ThrowIfNull(safetyEvent);
        lock (_gate)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
INSERT INTO safety_events (timestamp_utc, outcome, gate, detail, entity_id, domain, service, scenario_id, goal_id)
VALUES ($ts, $outcome, $gate, $detail, $entity, $domain, $service, $scenario, $goal);";
            cmd.Parameters.AddWithValue("$ts", safetyEvent.Timestamp.ToString("o", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$outcome", safetyEvent.Outcome);
            cmd.Parameters.AddWithValue("$gate", safetyEvent.Gate);
            cmd.Parameters.AddWithValue("$detail", safetyEvent.Detail);
            cmd.Parameters.AddWithValue("$entity", (object?)safetyEvent.EntityId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$domain", (object?)safetyEvent.Domain ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$service", (object?)safetyEvent.Service ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$scenario", (object?)safetyEvent.ScenarioId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$goal", (object?)safetyEvent.GoalId ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    // Most recent events (bounded by capacity), newest first.
    public IReadOnlyList<SafetyEventRecord> GetRecent()
    {
        lock (_gate)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
SELECT timestamp_utc, outcome, gate, detail, entity_id, domain, service, scenario_id, goal_id
FROM safety_events
ORDER BY id DESC
LIMIT $cap;";
            cmd.Parameters.AddWithValue("$cap", _capacity);

            var result = new List<SafetyEventRecord>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new SafetyEventRecord(
                    Timestamp: DateTimeOffset.Parse(
                        reader.GetString(0), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                    Outcome: reader.GetString(1),
                    Gate: reader.GetString(2),
                    Detail: reader.GetString(3),
                    EntityId: reader.IsDBNull(4) ? null : reader.GetString(4),
                    Domain: reader.IsDBNull(5) ? null : reader.GetString(5),
                    Service: reader.IsDBNull(6) ? null : reader.GetString(6),
                    ScenarioId: reader.IsDBNull(7) ? null : reader.GetString(7),
                    GoalId: reader.IsDBNull(8) ? null : reader.GetString(8)));
            }
            return result;
        }
    }

    private void Execute(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _connection.Dispose();
    }
}
