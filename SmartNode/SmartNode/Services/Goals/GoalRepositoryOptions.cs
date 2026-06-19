namespace SmartNode.Services.Goals;

// Parsed configuration for the goal-repository provider.
// Default is the JSON provider so existing behaviour and the offline demo are
// unchanged. Fail-fast: an unknown provider throws here (no silent fallback).
public sealed record GoalRepositoryOptions(
    string Provider,
    string JsonPath,
    string SqlitePath,
    string? SeedFromJsonPath)
{
    public const string ProviderEnvVar = "GOAL_REPOSITORY_PROVIDER";
    public const string JsonPathEnvVar = "GOAL_REPOSITORY_JSON_PATH";
    public const string SqlitePathEnvVar = "GOAL_REPOSITORY_SQLITE_PATH";
    public const string SeedFromJsonEnvVar = "GOAL_REPOSITORY_SEED_FROM_JSON";

    public const string ProviderJson = "json";
    public const string ProviderSqlite = "sqlite";
    public const string DefaultJsonPath = "config/user-goals.example.json";
    public const string DefaultSqlitePath = "data/goals.sqlite";

    public static GoalRepositoryOptions Parse(Func<string, string?> getEnv)
    {
        var rawProvider = (getEnv(ProviderEnvVar) ?? string.Empty).Trim();
        var provider = rawProvider.Length == 0 ? ProviderJson : rawProvider.ToLowerInvariant();

        if (provider != ProviderJson && provider != ProviderSqlite)
        {
            throw new ArgumentException(
                $"Unknown {ProviderEnvVar}='{rawProvider}'. Supported values: {ProviderJson}, {ProviderSqlite}.");
        }

        var rawJson = (getEnv(JsonPathEnvVar) ?? string.Empty).Trim();
        var jsonPath = rawJson.Length == 0 ? DefaultJsonPath : rawJson;

        var rawSqlite = (getEnv(SqlitePathEnvVar) ?? string.Empty).Trim();
        var sqlitePath = rawSqlite.Length == 0 ? DefaultSqlitePath : rawSqlite;

        var rawSeed = (getEnv(SeedFromJsonEnvVar) ?? string.Empty).Trim();
        var seedFromJsonPath = rawSeed.Length == 0 ? null : rawSeed;

        return new GoalRepositoryOptions(provider, jsonPath, sqlitePath, seedFromJsonPath);
    }
}
