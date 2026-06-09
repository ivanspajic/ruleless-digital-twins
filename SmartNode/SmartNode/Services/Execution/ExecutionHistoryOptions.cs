namespace SmartNode.Services.Execution;

// Parsed configuration for the execution-history provider. Default is now the durable SQLite
// provider so cooldown/rate-limit survive a restart by default; in-memory stays
// available explicitly via MAPEK_EXECUTION_HISTORY_PROVIDER=memory (tests/dev).
// Fail-fast: an unknown provider throws here (no silent fallback).
public sealed record ExecutionHistoryOptions(string Provider, string SqlitePath)
{
    public const string ProviderEnvVar = "MAPEK_EXECUTION_HISTORY_PROVIDER";
    public const string SqlitePathEnvVar = "MAPEK_EXECUTION_HISTORY_DB";

    public const string ProviderMemory = "memory";
    public const string ProviderSqlite = "sqlite";
    public const string DefaultSqlitePath = "data/execution-history.sqlite";

    public static ExecutionHistoryOptions Parse(Func<string, string?> getEnv)
    {
        var rawProvider = (getEnv(ProviderEnvVar) ?? string.Empty).Trim();
        var provider = rawProvider.Length == 0 ? ProviderSqlite : rawProvider.ToLowerInvariant();

        if (provider != ProviderMemory && provider != ProviderSqlite)
        {
            throw new ArgumentException(
                $"Unknown {ProviderEnvVar}='{rawProvider}'. Supported values: {ProviderMemory}, {ProviderSqlite}.");
        }

        var rawPath = (getEnv(SqlitePathEnvVar) ?? string.Empty).Trim();
        var sqlitePath = rawPath.Length == 0 ? DefaultSqlitePath : rawPath;

        return new ExecutionHistoryOptions(provider, sqlitePath);
    }
}
