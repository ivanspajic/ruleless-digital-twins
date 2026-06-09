using SmartNode.Models.MapeK;

namespace SmartNode.Mapek.Execution;

// Pure, fail-closed decision logic for real Home Assistant execution (step 1.6).
// No env reads, no I/O — every input is explicit, so the whole gate is unit
// testable. Real execution requires ALL master gates to hold AND each action to
// pass the operator allowlist.
public static class ExecutionPolicy
{
    // Master gate: is real execution even attempted this tick? All three must hold.
    public static bool RealExecutionEnabled(ExecutionSettings settings, bool requestedDryRun)
        => settings.AllowExecution && !requestedDryRun && settings.TokenPresent;

    // First failing master gate (for an explanatory warning), or null if enabled.
    public static string? BlockedReason(ExecutionSettings settings, bool requestedDryRun)
    {
        if (!settings.AllowExecution) return "MAPEK_ALLOW_EXECUTION is not enabled";
        if (requestedDryRun) return "request did not set dryRun=false";
        if (!settings.TokenPresent) return "TOKEN_HA is empty";
        return null;
    }

    // Live-snapshot presence gate (P4-G). A plan must never execute against an
    // entity that is not present in the active Home Assistant snapshot, even if
    // the operator allowlist is permissive. Fail-closed: a null/empty set means
    // presence cannot be confirmed, so nothing is executable. Case-insensitive
    // and independent of the passed set's comparer — HA entity_ids are canonical
    // lowercase, but goal/binding data may differ in casing.
    public static bool IsTargetPresent(HaAction action, IReadOnlySet<string>? presentEntities)
    {
        if (presentEntities is null || presentEntities.Count == 0) return false;
        foreach (var id in presentEntities)
        {
            if (string.Equals(id, action.EntityId, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    // Per-action allowlist check. Assumes RealExecutionEnabled is already true.
    // Fail-closed: an empty allowlist denies everything.
    public static (bool allowed, string reason) EvaluateAction(ExecutionSettings settings, HaAction action)
    {
        if (!settings.AllowedEntities.Contains(action.EntityId))
            return (false, $"entity '{action.EntityId}' not in MAPEK_ALLOWED_ENTITIES");

        var serviceKey = $"{action.Domain}.{action.Service}";
        if (!settings.AllowedServices.Contains(serviceKey))
            return (false, $"service '{serviceKey}' not in MAPEK_ALLOWED_SERVICES");

        return (true, "allowed by MAPEK allowlist");
    }
}
