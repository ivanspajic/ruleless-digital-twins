using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using SmartNode.Models.Goals;

namespace SmartNode.Services.Goals;

// Durable, SQLite-backed goal repository. Goals survive a
// restart (unlike the JSON provider, which is read-only from a file). Each goal
// is stored as the full UserGoal serialised to JSON (lossless, future-proof) plus
// queryable id/enabled/type columns. Stores no secrets. Optional, opt-in seed from
// a JSON sample — only when the table is empty (never overwrites existing goals).
// Fail-fast: the constructor throws a clear error if the database cannot be
// initialised, rather than silently falling back to the JSON provider.
public sealed class SqliteGoalRepository : IMutableGoalRepository, IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new();

    private readonly object _gate = new();
    private readonly SqliteConnection _connection;
    private readonly ILogger<GoalRepository>? _logger;
    private bool _disposed;

    public SqliteGoalRepository(string databasePath, string? seedFromJsonPath = null, ILogger<GoalRepository>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Goal repository SQLite path must not be empty.", nameof(databasePath));
        }

        _logger = logger;
        var fullPath = Path.GetFullPath(databasePath);
        try
        {
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var connectionString = new SqliteConnectionStringBuilder { DataSource = fullPath }.ToString();
            _connection = new SqliteConnection(connectionString);
            _connection.Open();

            Execute("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;");
            Execute(@"
CREATE TABLE IF NOT EXISTS goals (
    id         TEXT    PRIMARY KEY,
    enabled    INTEGER NOT NULL,
    type       TEXT    NULL,
    goal_json  TEXT    NOT NULL,
    created_at TEXT    NOT NULL,
    updated_at TEXT    NOT NULL
);");
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new InvalidOperationException(
                $"Failed to initialise the SQLite goal repository at '{fullPath}'. " +
                $"Check {GoalRepositoryOptions.SqlitePathEnvVar} and filesystem permissions. ({ex.Message})", ex);
        }

        if (!string.IsNullOrWhiteSpace(seedFromJsonPath))
        {
            SeedFromJsonIfEmpty(seedFromJsonPath!);
        }
    }

    public IReadOnlyList<UserGoal> GetAll()
    {
        lock (_gate)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT goal_json FROM goals ORDER BY id;";

            var result = new List<UserGoal>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                var goal = JsonSerializer.Deserialize<UserGoal>(reader.GetString(0), JsonOptions);
                if (goal is not null) result.Add(goal);
            }
            return result;
        }
    }

    public UserGoal? GetById(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        lock (_gate)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT goal_json FROM goals WHERE id = $id LIMIT 1;";
            cmd.Parameters.AddWithValue("$id", id);
            var json = cmd.ExecuteScalar() as string;
            return json is null ? null : JsonSerializer.Deserialize<UserGoal>(json, JsonOptions);
        }
    }

    public bool Delete(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        lock (_gate)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM goals WHERE id = $id;";
            cmd.Parameters.AddWithValue("$id", id);
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    // Insert or replace a goal by id. Public so the goal editor / a future CLI can use it.
    public void Upsert(UserGoal goal)
    {
        ArgumentNullException.ThrowIfNull(goal);
        var now = DateTimeOffset.UtcNow.ToString("o");
        lock (_gate)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
INSERT INTO goals (id, enabled, type, goal_json, created_at, updated_at)
VALUES ($id, $enabled, $type, $json, $now, $now)
ON CONFLICT(id) DO UPDATE SET
    enabled    = excluded.enabled,
    type       = excluded.type,
    goal_json  = excluded.goal_json,
    updated_at = excluded.updated_at;";
            cmd.Parameters.AddWithValue("$id", goal.Id);
            cmd.Parameters.AddWithValue("$enabled", goal.Enabled ? 1 : 0);
            cmd.Parameters.AddWithValue("$type", (object?)goal.Type ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$json", JsonSerializer.Serialize(goal, JsonOptions));
            cmd.Parameters.AddWithValue("$now", now);
            cmd.ExecuteNonQuery();
        }
    }

    private int Count()
    {
        lock (_gate)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM goals;";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
    }

    // Opt-in seed: import goals from a JSON sample, but only when the table is
    // empty — existing goals are never overwritten. Reuses GoalRepository's JSON
    // parsing + credential-marker filtering.
    private void SeedFromJsonIfEmpty(string seedJsonPath)
    {
        if (Count() > 0) return;

        var resolved = JsonGoalRepository.Resolve(seedJsonPath);
        if (resolved is null)
        {
            _logger?.LogWarning(
                "Goal seed file '{path}' not found; SQLite goals table left empty.", seedJsonPath);
            return;
        }

        var json = new GoalRepository(_logger);
        json.LoadFromFile(resolved);

        var seeded = 0;
        foreach (var goal in json.GetAll())
        {
            Upsert(goal);
            seeded++;
        }
        _logger?.LogInformation(
            "Seeded {n} goal(s) into the SQLite goal repository from '{path}'.", seeded, resolved);
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
