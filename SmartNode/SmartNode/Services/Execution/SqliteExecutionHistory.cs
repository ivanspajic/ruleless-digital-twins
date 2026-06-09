using System.Globalization;
using Microsoft.Data.Sqlite;
using SmartNode.Mapek.Execution;

namespace SmartNode.Services.Execution;

// Durable, SQLite-backed execution history. Persists every
// successful real execution so cooldown / rate-limit survive a SmartNode restart
// (unlike InMemoryExecutionHistory). Thread-safe via a single guarded connection.
// Stores NO secrets — only entity/domain/service, a UTC timestamp, and optional
// scenario/goal ids. Fail-fast: the constructor throws a clear error if the
// database cannot be created/opened, so a misconfigured path is reported at
// startup rather than silently losing the safety history.
public sealed class SqliteExecutionHistory : IExecutionHistory, IDisposable
{
    private readonly int _capacity;
    private readonly object _gate = new();
    private readonly SqliteConnection _connection;
    private bool _disposed;

    public SqliteExecutionHistory(string databasePath, int capacity = 1000)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Execution history SQLite path must not be empty.", nameof(databasePath));
        }
        _capacity = capacity > 0 ? capacity : 1000;

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
CREATE TABLE IF NOT EXISTS execution_history (
    id            INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp_utc TEXT    NOT NULL,
    entity_id     TEXT    NOT NULL,
    domain        TEXT    NOT NULL,
    service       TEXT    NOT NULL,
    scenario_id   TEXT    NULL,
    goal_id       TEXT    NULL
);");
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new InvalidOperationException(
                $"Failed to initialise the SQLite execution history at '{fullPath}'. " +
                $"Check {ExecutionHistoryOptions.SqlitePathEnvVar} and filesystem permissions. ({ex.Message})", ex);
        }
    }

    public void Record(ActionExecutionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        lock (_gate)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
INSERT INTO execution_history (timestamp_utc, entity_id, domain, service, scenario_id, goal_id)
VALUES ($ts, $entity, $domain, $service, $scenario, $goal);";
            cmd.Parameters.AddWithValue("$ts", record.ExecutedAt.ToString("o", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$entity", record.EntityId);
            cmd.Parameters.AddWithValue("$domain", record.Domain);
            cmd.Parameters.AddWithValue("$service", record.Service);
            cmd.Parameters.AddWithValue("$scenario", (object?)record.ScenarioId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$goal", (object?)record.GoalId ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    // Most recent records (bounded by capacity). Order is not part of the contract:
    // the time-windowed safety policies scan and filter by their own window.
    public IReadOnlyList<ActionExecutionRecord> GetRecent()
    {
        lock (_gate)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
SELECT timestamp_utc, entity_id, domain, service, scenario_id, goal_id
FROM execution_history
ORDER BY id DESC
LIMIT $cap;";
            cmd.Parameters.AddWithValue("$cap", _capacity);

            var result = new List<ActionExecutionRecord>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new ActionExecutionRecord(
                    EntityId: reader.GetString(1),
                    Domain: reader.GetString(2),
                    Service: reader.GetString(3),
                    ExecutedAt: DateTimeOffset.Parse(
                        reader.GetString(0), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                    ScenarioId: reader.IsDBNull(4) ? null : reader.GetString(4),
                    GoalId: reader.IsDBNull(5) ? null : reader.GetString(5)));
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
