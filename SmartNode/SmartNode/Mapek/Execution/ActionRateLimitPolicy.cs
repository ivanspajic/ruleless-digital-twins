using SmartNode.Models.MapeK;

namespace SmartNode.Mapek.Execution;

// Sliding-window (1 hour) rate limits over real executions, with two independent
// caps: global (MaxActionsPerHour) and per-entity (MaxActionsPerEntityPerHour).
// 0 disables a cap. Pure: the history and `now` are explicit, so there is no
// clock and no mutable state. The global cap is checked first.
public static class ActionRateLimitPolicy
{
    private static readonly TimeSpan Window = TimeSpan.FromHours(1);

    public static SafetyDecision Evaluate(
        SafetyRuntimeOptions options,
        HaAction action,
        IReadOnlyList<ActionExecutionRecord> history,
        DateTimeOffset now)
    {
        var cutoff = now - Window;

        if (options.MaxActionsPerHour > 0)
        {
            var total = 0;
            foreach (var r in history)
                if (InWindow(r, cutoff, now)) total++;

            if (total >= options.MaxActionsPerHour)
                return SafetyDecision.Reject(
                    SafetyRejectionReason.GlobalRateLimit,
                    $"global rate limit reached: {total}/{options.MaxActionsPerHour} " +
                    "action(s) in the last hour (MAPEK_MAX_ACTIONS_PER_HOUR)");
        }

        if (options.MaxActionsPerEntityPerHour > 0)
        {
            var perEntity = 0;
            foreach (var r in history)
                if (InWindow(r, cutoff, now)
                    && string.Equals(r.EntityId, action.EntityId, StringComparison.OrdinalIgnoreCase))
                    perEntity++;

            if (perEntity >= options.MaxActionsPerEntityPerHour)
                return SafetyDecision.Reject(
                    SafetyRejectionReason.EntityRateLimit,
                    $"per-entity rate limit reached for {action.EntityId}: " +
                    $"{perEntity}/{options.MaxActionsPerEntityPerHour} in the last hour " +
                    "(MAPEK_MAX_ACTIONS_PER_ENTITY_PER_HOUR)");
        }

        return SafetyDecision.Allow;
    }

    private static bool InWindow(ActionExecutionRecord r, DateTimeOffset cutoff, DateTimeOffset now)
        => r.ExecutedAt > cutoff && r.ExecutedAt <= now;
}
