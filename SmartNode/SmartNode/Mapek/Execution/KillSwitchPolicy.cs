namespace SmartNode.Mapek.Execution;

// Global kill switch. When engaged, NO real Home Assistant execution may proceed
// this tick, regardless of every other gate (allow flag, token, allowlist,
// presence). Pure and stateless. Dry-run planning is unaffected.
public static class KillSwitchPolicy
{
    public const string ReasonText = "MAPEK_KILL_SWITCH is engaged";

    public static bool BlocksRealExecution(SafetyRuntimeOptions options)
        => options.KillSwitchEngaged;
}
