namespace SmartNode.Services.Settings;

// Parsed configuration for the non-secret product settings store.
// Default = memory, so no runtime file is created unless SQLite is explicitly
// selected. Fail-fast on unknown providers; never silently fall back.
public sealed record SettingsStoreOptions(string Provider, string SqlitePath)
{
    public const string ProviderEnvVar = "SETTINGS_STORE_PROVIDER";
    public const string SqlitePathEnvVar = "SETTINGS_STORE_SQLITE_PATH";

    public const string ProviderMemory = "memory";
    public const string ProviderSqlite = "sqlite";
    public const string DefaultSqlitePath = "data/settings.sqlite";

    public static SettingsStoreOptions Parse(Func<string, string?> getEnv)
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

        return new SettingsStoreOptions(provider, sqlitePath);
    }
}
