using SmartNode.Mapek.Monitoring;
using SmartNode.Models.Goals;
using SmartNode.Models.MapeK;

namespace SmartNode.Mapek.Analysis;

// Minimal MAPE-K Analyzer (issue #59). Inspects the observed RuntimeState, the
// active goals, and the selected plan, and returns stable-coded findings. This
// implementation is intentionally read-only and side-effect-free: no HA call,
// no mutation, no autonomous decision. Future PRs will plug in goal-condition
// evaluation against the observed snapshot/price.
public sealed class MapekAnalyzerService : IMapekAnalyzerService
{
    public const string SeverityInfo = "info";
    public const string SeverityWarning = "warning";

    public const string CodeHaSnapshotUnavailable = "ha-snapshot-unavailable";
    public const string CodeHaSnapshotAvailable = "ha-snapshot-available";
    public const string CodePriceProviderUnavailable = "price-provider-unavailable";
    public const string CodeCurrentPriceAvailable = "current-price-available";
    public const string CodeCurrentPriceMissingSourceKnown = "current-price-missing-but-source-known";
    public const string CodeNoActiveGoals = "no-active-goals";
    public const string CodeActiveGoalsNotPlanned = "active-goals-not-planned";
    public const string CodeNoAutonomousPlan = "no-autonomous-plan";
    public const string CodeNoHaActionExecuted = "no-ha-action-executed";
    public const string CodeGoalTargetNotInSnapshot = "goal-target-not-in-snapshot";

    public AnalysisResult Analyze(
        RuntimeState observedState,
        IReadOnlyList<UserGoal> activeGoals,
        ActionPlan selectedPlan)
    {
        var findings = new List<AnalysisFinding>();

        AddHaSnapshotFinding(observedState, findings);
        AddPriceFinding(observedState, findings);
        AddGoalsFinding(activeGoals, findings);
        AddPlanFindings(selectedPlan, observedState, findings);

        return new AnalysisResult(findings);
    }

    private static void AddHaSnapshotFinding(RuntimeState state, List<AnalysisFinding> findings)
    {
        if (state.HaEntitiesSnapshot.Count == 0)
        {
            findings.Add(new AnalysisFinding(
                CodeHaSnapshotUnavailable,
                SeverityWarning,
                "Home Assistant state snapshot is empty or unavailable."));
        }
        else
        {
            findings.Add(new AnalysisFinding(
                CodeHaSnapshotAvailable,
                SeverityInfo,
                $"Observed {state.HaEntitiesSnapshot.Count} Home Assistant entit{(state.HaEntitiesSnapshot.Count == 1 ? "y" : "ies")} from the live snapshot."));
        }
    }

    private static void AddPriceFinding(RuntimeState state, List<AnalysisFinding> findings)
    {
        // Treat null/empty/whitespace PriceSource as unavailable too — a missing
        // source string is just as uninformative as the explicit "unavailable"
        // marker, and emitting "missing-but-source-known" with no source name
        // would be misleading. (CodeRabbit feedback on PR #60.)
        var priceSourceUnavailable =
            string.IsNullOrWhiteSpace(state.PriceSource) ||
            string.Equals(
                state.PriceSource,
                MapekMonitorService.PriceSourceUnavailable,
                StringComparison.Ordinal);

        if (priceSourceUnavailable)
        {
            findings.Add(new AnalysisFinding(
                CodePriceProviderUnavailable,
                SeverityWarning,
                "No price provider observation available; current price could not be determined."));
            return;
        }

        if (state.CurrentPriceNokPerKwh.HasValue)
        {
            findings.Add(new AnalysisFinding(
                CodeCurrentPriceAvailable,
                SeverityInfo,
                $"Current price {state.CurrentPriceNokPerKwh.Value} observed from source '{state.PriceSource}'."));
        }
        else
        {
            findings.Add(new AnalysisFinding(
                CodeCurrentPriceMissingSourceKnown,
                SeverityWarning,
                $"Price source '{state.PriceSource}' is known but no slot covers the present instant; current price unset."));
        }
    }

    private static void AddGoalsFinding(IReadOnlyList<UserGoal> activeGoals, List<AnalysisFinding> findings)
    {
        if (activeGoals.Count == 0)
        {
            findings.Add(new AnalysisFinding(
                CodeNoActiveGoals,
                SeverityInfo,
                "No active user goals; analyzer has nothing to evaluate."));
        }
        else
        {
            findings.Add(new AnalysisFinding(
                CodeActiveGoalsNotPlanned,
                SeverityInfo,
                $"Observed {activeGoals.Count} active user goal(s); no autonomous planner runs against them in this dry-run path yet."));
        }
    }

    private static void AddPlanFindings(ActionPlan selectedPlan, RuntimeState observedState, List<AnalysisFinding> findings)
    {
        if (selectedPlan.Actions.Count == 0)
        {
            findings.Add(new AnalysisFinding(
                CodeNoAutonomousPlan,
                SeverityInfo,
                "Selected plan contains no actions; autonomous planning is not implemented in this dry-run path."));
        }

        AddGoalTargetPresenceFindings(selectedPlan, observedState, findings);

        findings.Add(new AnalysisFinding(
            CodeNoHaActionExecuted,
            SeverityInfo,
            "Dry-run mode forced: no Home Assistant service call was issued from this tick."));
    }

    // P4-G — a staged action whose target entity is absent from the live snapshot
    // is not executable against the active HA profile. We surface it as a warning
    // already in dry-run so the misalignment (e.g. a demo_* goal target against a
    // showcase_* HA) is visible before any real-execution attempt. Only evaluated
    // when the snapshot is actually populated — an empty snapshot is already
    // reported by ha-snapshot-unavailable, and flagging every action then would be
    // redundant noise.
    private static void AddGoalTargetPresenceFindings(
        ActionPlan selectedPlan, RuntimeState observedState, List<AnalysisFinding> findings)
    {
        if (selectedPlan.Actions.Count == 0 || observedState.HaEntitiesSnapshot.Count == 0)
            return;

        var present = new HashSet<string>(
            observedState.HaEntitiesSnapshot.Keys, StringComparer.OrdinalIgnoreCase);

        foreach (var action in selectedPlan.Actions)
        {
            if (present.Contains(action.EntityId)) continue;

            findings.Add(new AnalysisFinding(
                CodeGoalTargetNotInSnapshot,
                SeverityWarning,
                $"Planned action {action.Domain}.{action.Service} targets '{action.EntityId}', " +
                $"which is absent from the live Home Assistant snapshot ({observedState.HaEntitiesSnapshot.Count} entities); " +
                "not executable against the active profile."));
        }
    }
}
