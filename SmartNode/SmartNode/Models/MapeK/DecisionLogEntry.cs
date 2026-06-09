namespace SmartNode.Models.MapeK;

// Compact, human-readable trace of a single MAPE-K tick decision (step 1.7).
// Deliberately lighter than MapeKDecision: it captures what the decision *was*
// (goal, winning scenario, observed price, staged actions, dry-run flag,
// explanation) without dumping the full RuntimeState or every scenario, so a
// rolling log stays readable and cheap to expose over HTTP.
public sealed record DecisionLogEntry(
    string Timestamp,
    string? GoalId,
    string SelectedScenario,
    double? ObservedPriceNokPerKwh,
    IReadOnlyList<HaAction> Actions,
    bool DryRun,
    string Explanation
);
