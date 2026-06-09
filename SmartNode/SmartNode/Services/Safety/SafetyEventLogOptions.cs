namespace SmartNode.Services.Safety;

// Parsed configuration for the safety-event audit log provider. Default is the
// transient in-memory provider so a default run creates no new database file and
// existing deployments are unchanged. Set MAPEK_SAFETY_EVENT_LOG_PROVIDER=sqlite
// for a durable audit trail that survives a restart (recommended before
// autonomous real execution). Fail-fast: an unknown provider throws here (no
// silent fallback).
public sealed record SafetyEventLogOptions(string Provider, string SqlitePath)
{
    public const string ProviderEnvVar = "MAPEK_SAFETY_EVENT_LOG_PROVIDER";
    public const string SqlitePathEnvVar = "MAPEK_SAFETY_EVENT_LOG_DB";

    public const string ProviderMemory = "memory";
    public const string ProviderSqlite = "sqlite";
    public const string DefaultSqlitePath = "data/safety-events.sqlite";

    public static SafetyEventLogOptions Parse(Func<string, string?> getEnv)
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

        return new SafetyEventLogOptions(provider, sqlitePath);
    }
}
