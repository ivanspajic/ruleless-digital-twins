using SmartNode.Mapek.Execution;

namespace SmartNode.Services.Safety;

public sealed record SafetyEndpointResult(int StatusCode, object Payload);

// Read-only projection of the current safety posture plus the recent audit
// trail (P2). Secrets-free by construction: it exposes booleans, integer limits,
// and allowlist *counts* — never the Home Assistant token or the allowlisted
// entity/service names. The audit events themselves carry no secrets.
public sealed record SafetyState(
    bool KillSwitchEngaged,
    bool AllowExecution,
    bool TokenPresent,
    int ActionCooldownSeconds,
    int MaxActionsPerHour,
    int MaxActionsPerEntityPerHour,
    int AllowedEntityCount,
    int AllowedServiceCount,
    string EventLogProvider);

// Pure builder for GET /api/safety — no env reads, no I/O beyond reading the
// audit log back, so it is fully unit-testable. Events are returned newest-first
// and capped to `maxEvents`.
public static class SafetyEndpoint
{
    public static SafetyEndpointResult Get(
        SafetyRuntimeOptions safety,
        ExecutionSettings execution,
        ISafetyEventLog eventLog,
        string eventLogProvider,
        int maxEvents = 100)
    {
        ArgumentNullException.ThrowIfNull(safety);
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(eventLog);

        var state = new SafetyState(
            KillSwitchEngaged: safety.KillSwitchEngaged,
            AllowExecution: execution.AllowExecution,
            TokenPresent: execution.TokenPresent,
            ActionCooldownSeconds: safety.ActionCooldownSeconds,
            MaxActionsPerHour: safety.MaxActionsPerHour,
            MaxActionsPerEntityPerHour: safety.MaxActionsPerEntityPerHour,
            AllowedEntityCount: execution.AllowedEntities.Count,
            AllowedServiceCount: execution.AllowedServices.Count,
            EventLogProvider: eventLogProvider ?? "unknown");

        var cap = maxEvents > 0 ? maxEvents : 100;
        var events = eventLog.GetRecent()
            .OrderByDescending(e => e.Timestamp)
            .Take(cap)
            .ToList();

        return new SafetyEndpointResult(200, new
        {
            state,
            eventCount = events.Count,
            events
        });
    }
}
