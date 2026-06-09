using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using SmartNode.Services.Decisions;
using SmartNode.Services.Execution;
using SmartNode.Services.Goals;
using SmartNode.Services.Settings;

namespace SmartNode.Services.Health;

public sealed record HealthEndpointResult(int StatusCode, object Payload);

public sealed record HealthStatus(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("utc")] string Utc);

public sealed record ReadinessStatus(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("utc")] string Utc,
    [property: JsonPropertyName("checks")] IReadOnlyList<ReadinessCheck> Checks);

public sealed record ReadinessCheck(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("detail")] string Detail,
    [property: JsonPropertyName("path")] string? Path = null,
    [property: JsonPropertyName("error")] string? Error = null);

public static class HealthEndpoint
{
    public static HealthEndpointResult Health(Func<DateTimeOffset>? utcNow = null)
    {
        var now = (utcNow ?? (() => DateTimeOffset.UtcNow))();
        return new HealthEndpointResult(200, new HealthStatus("ok", now.ToString("o")));
    }

    public static HealthEndpointResult Ready(
        GoalRepositoryOptions goalOptions,
        DecisionLogOptions decisionOptions,
        ExecutionHistoryOptions executionOptions,
        SettingsStoreOptions settingsOptions,
        IGoalRepository goals,
        IDecisionLog decisions,
        IExecutionHistory executionHistory,
        ISettingsStore settings,
        Func<DateTimeOffset>? utcNow = null)
    {
        ArgumentNullException.ThrowIfNull(goalOptions);
        ArgumentNullException.ThrowIfNull(decisionOptions);
        ArgumentNullException.ThrowIfNull(executionOptions);
        ArgumentNullException.ThrowIfNull(settingsOptions);
        ArgumentNullException.ThrowIfNull(goals);
        ArgumentNullException.ThrowIfNull(decisions);
        ArgumentNullException.ThrowIfNull(executionHistory);
        ArgumentNullException.ThrowIfNull(settings);

        var checks = new List<ReadinessCheck>
        {
            CheckLocalStore(
                "goals",
                goalOptions.Provider,
                () => goals.GetAll(),
                goalOptions.Provider == GoalRepositoryOptions.ProviderSqlite ? goalOptions.SqlitePath : null),
            CheckLocalStore(
                "decisions",
                decisionOptions.Provider,
                () => decisions.GetRecent(1),
                decisionOptions.Provider == DecisionLogOptions.ProviderSqlite ? decisionOptions.SqlitePath : null),
            CheckLocalStore(
                "execution_history",
                executionOptions.Provider,
                () => executionHistory.GetRecent(),
                executionOptions.Provider == ExecutionHistoryOptions.ProviderSqlite ? executionOptions.SqlitePath : null),
            CheckLocalStore(
                "settings",
                settingsOptions.Provider,
                () => settings.GetAll(),
                settingsOptions.Provider == SettingsStoreOptions.ProviderSqlite ? settingsOptions.SqlitePath : null)
        };

        var ready = checks.All(c => c.Status == "ok");
        var now = (utcNow ?? (() => DateTimeOffset.UtcNow))();
        return new HealthEndpointResult(
            ready ? 200 : 503,
            new ReadinessStatus(ready ? "ready" : "not_ready", now.ToString("o"), checks));
    }

    private static ReadinessCheck CheckLocalStore(
        string name,
        string provider,
        Action probe,
        string? sqlitePath)
    {
        try
        {
            probe();
            var resolvedPath = string.IsNullOrWhiteSpace(sqlitePath)
                ? null
                : CheckSqlite(sqlitePath);

            return new ReadinessCheck(
                name,
                provider,
                "ok",
                resolvedPath is null ? "local provider accessible" : "SQLite database accessible",
                resolvedPath);
        }
        catch (Exception ex)
        {
            return new ReadinessCheck(
                name,
                provider,
                "error",
                "local provider unavailable",
                string.IsNullOrWhiteSpace(sqlitePath) ? null : Path.GetFullPath(sqlitePath),
                ex.Message);
        }
    }

    private static string CheckSqlite(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("SQLite path must not be empty.");
        }

        var fullPath = Path.GetFullPath(databasePath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("SQLite database file does not exist.", fullPath);
        }

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWrite
        }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1;";
        command.ExecuteScalar();
        return fullPath;
    }
}
