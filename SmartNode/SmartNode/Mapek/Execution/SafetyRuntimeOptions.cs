namespace SmartNode.Mapek.Execution;

// Immutable snapshot of the P7-A autonomy safety controls, built from the
// environment at request time and passed into the policies so they stay pure
// (no env reads, no clock inside the policy logic).
//
// Defaults are PERMISSIVE on purpose: wiring this in must not change any existing
// behaviour. Kill switch off; cooldown and rate limits 0 == disabled (unlimited).
// Enabling real autonomous actuation (P7-B) is a separate, later step.
public sealed record SafetyRuntimeOptions(
    bool KillSwitchEngaged,
    int ActionCooldownSeconds,
    int MaxActionsPerHour,
    int MaxActionsPerEntityPerHour)
{
    public static SafetyRuntimeOptions Permissive { get; } = new(false, 0, 0, 0);

    // Build from an injectable getter so this is unit-testable without touching
    // real process environment variables.
    public static SafetyRuntimeOptions FromEnvironment(Func<string, string?> getEnv)
        => new(
            KillSwitchEngaged: ParseBool(getEnv("MAPEK_KILL_SWITCH")),
            ActionCooldownSeconds: ParseNonNegative(getEnv("MAPEK_ACTION_COOLDOWN_SECONDS")),
            MaxActionsPerHour: ParseNonNegative(getEnv("MAPEK_MAX_ACTIONS_PER_HOUR")),
            MaxActionsPerEntityPerHour: ParseNonNegative(getEnv("MAPEK_MAX_ACTIONS_PER_ENTITY_PER_HOUR")));

    public static SafetyRuntimeOptions FromEnvironment()
        => FromEnvironment(Environment.GetEnvironmentVariable);

    private static bool ParseBool(string? raw)
    {
        var v = raw?.Trim();
        return v is not null
            && (v.Equals("1", StringComparison.OrdinalIgnoreCase)
                || v.Equals("true", StringComparison.OrdinalIgnoreCase)
                || v.Equals("yes", StringComparison.OrdinalIgnoreCase));
    }

    // Invalid / negative / missing → 0 (disabled). Never throws, so a malformed
    // env value can never crash a tick nor silently enable a stricter-than-asked
    // limit; it just leaves that control off.
    private static int ParseNonNegative(string? raw)
        => int.TryParse(raw?.Trim(), out var n) && n > 0 ? n : 0;
}
