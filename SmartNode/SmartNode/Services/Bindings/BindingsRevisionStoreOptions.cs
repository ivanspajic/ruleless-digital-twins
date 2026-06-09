namespace SmartNode.Services.Bindings;

// Parsed configuration for the bindings revision-store provider. Default is the
// transient in-memory provider so a default run creates no new database file and
// existing deployments are unchanged. Set BINDINGS_REVISION_STORE_PROVIDER=sqlite
// for a durable history that survives a restart. Fail-fast: an unknown provider
// throws here (no silent fallback), mirroring the other store options.
public sealed record BindingsRevisionStoreOptions(string Provider, string SqlitePath)
{
    public const string ProviderEnvVar = "BINDINGS_REVISION_STORE_PROVIDER";
    public const string SqlitePathEnvVar = "BINDINGS_REVISION_STORE_DB";

    public const string ProviderMemory = "memory";
    public const string ProviderSqlite = "sqlite";

    // Follows the existing store convention (data/safety-events.sqlite, ...).
    // Covered by the .gitignore *.sqlite rule, so the DB is never committed.
    public const string DefaultSqlitePath = "data/bindings-revisions.sqlite";

    public static BindingsRevisionStoreOptions Parse(Func<string, string?> getEnv)
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

        return new BindingsRevisionStoreOptions(provider, sqlitePath);
    }
}
