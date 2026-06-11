using SmartNode.Models.Goals;
using SmartNode.Models.Simulation;

namespace SmartNode.Models.MapeK;

public record MapeKDecision(
    DateTimeOffset Timestamp,
    bool DryRun,
    RuntimeState ObservedState,
    IReadOnlyList<UserGoal> ActiveGoals,
    IReadOnlyList<SimulationResult> SimulatedScenarios,
    ActionPlan SelectedPlan,
    string Explanation,
    IReadOnlyList<string> Warnings
);
