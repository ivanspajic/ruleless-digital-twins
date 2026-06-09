namespace SmartNode.Services.Decisions;

// Parsed configuration for the decision-log provider.
// Default is the in-memory provider so existing behaviour and the offline demo
// are unchanged. Fail-fast: an unknown provider throws here (no silent fallback).
public sealed record DecisionLogOptions(string Provider, string SqlitePath)
{
    public const string ProviderEnvVar = "DECISION_LOG_PROVIDER";
    public const string SqlitePathEnvVar = "DECISION_LOG_SQLITE_PATH";

    public const string ProviderMemory = "memory";
    public const string ProviderSqlite = "sqlite";
    public const string DefaultSqlitePath = "data/decision-log.sqlite";

    public static DecisionLogOptions Parse(Func<string, string?> getEnv)
    {
        var rawProvider = (getEnv(ProviderEnvVar) ?? string.Empty).Trim();
        var provider = rawProvider.Length == 0 ? ProviderMemory : rawProvider.ToLowerInvariant();

        if (provider != ProviderMemory && provider != ProviderSqlite)
        {
            throw new ArgumentException(
                $"Unknown {ProviderEnvVar}='{rawProvider}'. Supported values: {ProviderMemory}, {ProviderSqlite}.");
        }

        var rawPath = (getEnv(SqlitePathEnvVar) ?? string.Empty).Trim();
        var sqlitePath = rawPath.Length == 0 ? DefaultSqlitePath : rawPath;

        return new DecisionLogOptions(provider, sqlitePath);
    }
}
