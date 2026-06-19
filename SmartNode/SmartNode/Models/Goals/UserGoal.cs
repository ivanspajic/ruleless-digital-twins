namespace SmartNode.Models.Goals;

public record UserGoal(
    string Id,
    bool Enabled,
    string Type,
    GoalCondition Condition,
    GoalObjective Objective,
    GoalConstraints Constraints,
    IReadOnlyList<GoalAction> Actions
);

public record GoalCondition(
    string? TimeAfter = null,
    string? Presence = null
);

public record GoalObjective(
    string? Room = null,
    double? TargetTemperature = null
);

public record GoalConstraints(
    bool PreferLowPrice = false,
    double? MaxPriceNokPerKwh = null,
    bool DryRun = true
);

public record GoalAction(
    string Domain,
    string Service,
    string EntityId,
    IReadOnlyDictionary<string, object?> Data
);
