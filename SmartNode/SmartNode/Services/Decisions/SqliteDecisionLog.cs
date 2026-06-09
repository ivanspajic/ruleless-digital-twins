using System.Text.Json;
using Microsoft.Data.Sqlite;
using SmartNode.Models.MapeK;

namespace SmartNode.Services.Decisions;

// Durable, SQLite-backed decision log. Every decision is
// persisted so the history survives a SmartNode restart (unlike InMemoryDecisionLog).
// Thread-safe via a single guarded connection. Stores no secrets — only the same
// compact DecisionLogEntry fields the in-memory log exposes (no TOKEN_HA, no
// Home Assistant credentials). Fail-fast: the constructor throws a clear error if
// the database cannot be created/opened, so a misconfigured sqlite path is
// reported at startup rather than silently falling back to memory.
public sealed class SqliteDecisionLog : IDecisionLog, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly object _gate = new();
    private readonly SqliteConnection _connection;
    private bool _disposed;

    public SqliteDecisionLog(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Decision log SQLite path must not be empty.", nameof(databasePath));
        }

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
CREATE TABLE IF NOT EXISTS decisions (
    id                INTEGER PRIMARY KEY AUTOINCREMENT,
    timestamp         TEXT    NOT NULL,
    goal_id           TEXT    NULL,
    selected_scenario TEXT    NOT NULL,
    observed_price    REAL    NULL,
    dry_run           INTEGER NOT NULL,
    explanation       TEXT    NOT NULL,
    actions_json      TEXT    NOT NULL
);");
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new InvalidOperationException(
                $"Failed to initialise the SQLite decision log at '{fullPath}'. " +
                $"Check {DecisionLogOptions.SqlitePathEnvVar} and filesystem permissions. ({ex.Message})", ex);
        }
    }

    public void Append(DecisionLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
INSERT INTO decisions (timestamp, goal_id, selected_scenario, observed_price, dry_run, explanation, actions_json)
VALUES ($ts, $goal, $scenario, $price, $dry, $expl, $actions);";
            cmd.Parameters.AddWithValue("$ts", entry.Timestamp);
            cmd.Parameters.AddWithValue("$goal", (object?)entry.GoalId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$scenario", entry.SelectedScenario);
            cmd.Parameters.AddWithValue("$price", (object?)entry.ObservedPriceNokPerKwh ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$dry", entry.DryRun ? 1 : 0);
            cmd.Parameters.AddWithValue("$expl", entry.Explanation);
            cmd.Parameters.AddWithValue("$actions", JsonSerializer.Serialize(entry.Actions, JsonOptions));
            cmd.ExecuteNonQuery();
        }
    }

    public IReadOnlyList<DecisionLogEntry> GetRecent(int max = 20)
    {
        if (max <= 0) return Array.Empty<DecisionLogEntry>();
        lock (_gate)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
SELECT timestamp, goal_id, selected_scenario, observed_price, dry_run, explanation, actions_json
FROM decisions
ORDER BY id DESC
LIMIT $max;";
            cmd.Parameters.AddWithValue("$max", max);

            var result = new List<DecisionLogEntry>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var actions = JsonSerializer.Deserialize<List<HaAction>>(reader.GetString(6), JsonOptions)
                    ?? new List<HaAction>();
                result.Add(new DecisionLogEntry(
                    Timestamp: reader.GetString(0),
                    GoalId: reader.IsDBNull(1) ? null : reader.GetString(1),
                    SelectedScenario: reader.GetString(2),
                    ObservedPriceNokPerKwh: reader.IsDBNull(3) ? null : reader.GetDouble(3),
                    Actions: actions,
                    DryRun: reader.GetInt64(4) != 0,
                    Explanation: reader.GetString(5)));
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
