using System.Reflection;
using SmartNode.Mapek.Execution;
using SmartNode.Services.Decisions;
using SmartNode.Services.Execution;
using SmartNode.Services.Goals;
using SmartNode.Services.HomeAssistant;
using SmartNode.Services.Safety;
using SmartNode.Services.Settings;

namespace SmartNode.Services.Product;

public sealed record ProductStatusStore(
    string Provider,
    string? Path);

public sealed record ProductStatusPersistence(
    ProductStatusStore Goals,
    ProductStatusStore Decisions,
    ProductStatusStore Settings,
    ProductStatusStore ExecutionHistory,
    ProductStatusStore SafetyEvents);

public sealed record ProductStatusHomeAssistant(
    bool Configured,
    bool UrlSet,
    bool TokenSet,
    bool Connected,
    string Source);

public sealed record ProductStatusAutonomous(
    bool Requested,
    bool RealExecutionRequested,
    bool DryRunOnly);

public sealed record ProductStatusSafety(
    bool AllowExecution,
    bool KillSwitchEngaged,
    bool TokenPresent,
    int AllowedEntityCount,
    int AllowedServiceCount,
    int ActionCooldownSeconds,
    int MaxActionsPerHour,
    int MaxActionsPerEntityPerHour,
    string EventLogProvider);

public sealed record ProductStatusBuild(
    string? Version,
    string? InformationalVersion);

internal static class ProductStatusEndpoint
{
    public static ProductApiResult Get(
        string appMode,
        bool autonomousRequested,
        HaConnection haConnection,
        GoalRepositoryOptions goalOptions,
        DecisionLogOptions decisionOptions,
        SettingsStoreOptions settingsOptions,
        ExecutionHistoryOptions executionOptions,
        SafetyEventLogOptions safetyEventOptions,
        ResolvedSafetySettings resolvedSafety,
        Assembly? assembly = null)
    {
        ArgumentNullException.ThrowIfNull(haConnection);
        ArgumentNullException.ThrowIfNull(goalOptions);
        ArgumentNullException.ThrowIfNull(decisionOptions);
        ArgumentNullException.ThrowIfNull(settingsOptions);
        ArgumentNullException.ThrowIfNull(executionOptions);
        ArgumentNullException.ThrowIfNull(safetyEventOptions);
        ArgumentNullException.ThrowIfNull(resolvedSafety);

        var snap = haConnection.GetSnapshot();
        assembly ??= Assembly.GetExecutingAssembly();

        var payload = new
        {
            appMode = string.IsNullOrWhiteSpace(appMode) ? "unknown" : appMode,
            persistence = new ProductStatusPersistence(
                Goals: new ProductStatusStore(goalOptions.Provider,
                    goalOptions.Provider == GoalRepositoryOptions.ProviderSqlite
                        ? SafePath(goalOptions.SqlitePath)
                        : SafePath(goalOptions.JsonPath)),
                Decisions: new ProductStatusStore(decisionOptions.Provider,
                    decisionOptions.Provider == DecisionLogOptions.ProviderSqlite
                        ? SafePath(decisionOptions.SqlitePath)
                        : null),
                Settings: new ProductStatusStore(settingsOptions.Provider,
                    settingsOptions.Provider == SettingsStoreOptions.ProviderSqlite
                        ? SafePath(settingsOptions.SqlitePath)
                        : null),
                ExecutionHistory: new ProductStatusStore(executionOptions.Provider,
                    executionOptions.Provider == ExecutionHistoryOptions.ProviderSqlite
                        ? SafePath(executionOptions.SqlitePath)
                        : null),
                SafetyEvents: new ProductStatusStore(safetyEventOptions.Provider,
                    safetyEventOptions.Provider == SafetyEventLogOptions.ProviderSqlite
                        ? SafePath(safetyEventOptions.SqlitePath)
                        : null)),
            homeAssistant = new ProductStatusHomeAssistant(
                Configured: !string.IsNullOrWhiteSpace(snap.Url) && snap.TokenSet,
                UrlSet: !string.IsNullOrWhiteSpace(snap.Url),
                TokenSet: snap.TokenSet,
                Connected: snap.LastConnected,
                Source: snap.Source),
            autonomous = new ProductStatusAutonomous(
                Requested: autonomousRequested,
                RealExecutionRequested: resolvedSafety.AutonomousExecutionEnabled,
                DryRunOnly: true),
            safety = new ProductStatusSafety(
                AllowExecution: resolvedSafety.Execution.AllowExecution,
                KillSwitchEngaged: resolvedSafety.Safety.KillSwitchEngaged,
                TokenPresent: resolvedSafety.Execution.TokenPresent,
                AllowedEntityCount: resolvedSafety.Execution.AllowedEntities.Count,
                AllowedServiceCount: resolvedSafety.Execution.AllowedServices.Count,
                ActionCooldownSeconds: resolvedSafety.Safety.ActionCooldownSeconds,
                MaxActionsPerHour: resolvedSafety.Safety.MaxActionsPerHour,
                MaxActionsPerEntityPerHour: resolvedSafety.Safety.MaxActionsPerEntityPerHour,
                EventLogProvider: safetyEventOptions.Provider),
            build = new ProductStatusBuild(
                Version: assembly.GetName().Version?.ToString(),
                InformationalVersion: assembly
                    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion)
        };

        return new ProductApiResult(200, payload);
    }

    internal static string? SafePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;

        var normalized = path.Replace('\\', '/').Trim();
        if (!Path.IsPathRooted(path))
        {
            return normalized;
        }

        var fileName = Path.GetFileName(path);
        return string.IsNullOrWhiteSpace(fileName) ? "<redacted>" : $"<redacted>/{fileName}";
    }
}
