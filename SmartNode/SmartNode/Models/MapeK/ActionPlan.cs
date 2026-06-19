namespace SmartNode.Models.MapeK;

public record ActionPlan(
    string ScenarioId,
    string Rationale,
    IReadOnlyList<HaAction> Actions,
    bool DryRun = true
);

public record HaAction(
    string Domain,
    string Service,
    string EntityId,
    IReadOnlyDictionary<string, object?> Data,
    bool Executed = false
);
