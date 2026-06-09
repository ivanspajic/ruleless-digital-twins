namespace SmartNode.Mapek.Execution;

// One audit entry for a real-execution safety decision (P1 / criterion 10 —
// audit log). Unlike ActionExecutionRecord (which records only *successful* real
// executions for cooldown/rate-limit), this captures the full safety trail:
// every blocked attempt, every failure, and every successful actuation, with the
// gate that decided it. Pure data, no secrets — entity/domain/service, a UTC
// timestamp, a human-readable detail, and optional scenario/goal context.
public sealed record SafetyEventRecord(
    DateTimeOffset Timestamp,
    string Outcome,
    string Gate,
    string Detail,
    string? EntityId = null,
    string? Domain = null,
    string? Service = null,
    string? ScenarioId = null,
    string? GoalId = null);

// Stable string constants for the audit columns, so producers and tests never
// drift on magic strings.
public static class SafetyEventOutcome
{
    public const string Blocked = "blocked";
    public const string Failed = "failed";
    public const string Executed = "executed";
}

public static class SafetyGate
{
    public const string MasterGate = "MasterGate";          // MAPEK_ALLOW_EXECUTION / dryRun / TOKEN_HA
    public const string KillSwitch = "KillSwitch";
    public const string HistoryUnavailable = "HistoryUnavailable";
    public const string TargetAbsent = "TargetAbsent";      // P4-G live-snapshot presence
    public const string Allowlist = "Allowlist";
    public const string Cooldown = "Cooldown";
    public const string RateLimit = "RateLimit";
    public const string Execution = "Execution";            // executor success/failure
}
