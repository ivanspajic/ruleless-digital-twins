using SmartNode.Models.MapeK;

namespace SmartNode.Mapek.Execution;

// Per entity+service cooldown. Blocks a real execution when the same entity AND
// service was executed within ActionCooldownSeconds of `now`. 0 disables it.
// Pure: the history and `now` are explicit, so there is no clock and no mutable
// state — the same inputs always yield the same decision.
public static class ActionCooldownPolicy
{
    public static SafetyDecision Evaluate(
        SafetyRuntimeOptions options,
        HaAction action,
        IReadOnlyList<ActionExecutionRecord> history,
        DateTimeOffset now)
    {
        if (options.ActionCooldownSeconds <= 0) return SafetyDecision.Allow;

        var window = TimeSpan.FromSeconds(options.ActionCooldownSeconds);

        // The cooldown is driven by the MOST RECENT execution of the same target.
        // Picking the newest (not the first encountered) guarantees the reported
        // "remaining" seconds is accurate when the history holds several matches.
        DateTimeOffset? newest = null;
        foreach (var record in history)
        {
            if (!SameTarget(record, action)) continue;
            if (record.ExecutedAt > now) continue; // ignore future-dated records
            if (newest is null || record.ExecutedAt > newest.Value) newest = record.ExecutedAt;
        }

        if (newest is not null)
        {
            var elapsed = now - newest.Value;
            if (elapsed < window)
            {
                var remaining = (int)Math.Ceiling((window - elapsed).TotalSeconds);
                return SafetyDecision.Reject(
                    SafetyRejectionReason.Cooldown,
                    $"cooldown active for {action.EntityId} {action.Domain}.{action.Service}: " +
                    $"{remaining}s remaining (MAPEK_ACTION_COOLDOWN_SECONDS={options.ActionCooldownSeconds})");
            }
        }

        return SafetyDecision.Allow;
    }

    private static bool SameTarget(ActionExecutionRecord record, HaAction action)
        => string.Equals(record.EntityId, action.EntityId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(record.Domain, action.Domain, StringComparison.OrdinalIgnoreCase)
        && string.Equals(record.Service, action.Service, StringComparison.OrdinalIgnoreCase);
}
