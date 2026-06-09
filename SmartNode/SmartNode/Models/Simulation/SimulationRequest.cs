using SmartNode.Models.Goals;
using SmartNode.Models.MapeK;

namespace SmartNode.Models.Simulation;

public record SimulationRequest(
    DateTimeOffset Timestamp,
    bool DryRun,
    RuntimeState ObservedState,
    IReadOnlyList<UserGoal> ActiveGoals
);
