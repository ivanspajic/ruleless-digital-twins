namespace SmartNode.Models.Simulation;

public record SimulationResult(
    string ScenarioId,
    string Description,
    double Score,
    bool Heuristic = false,
    IReadOnlyList<string>? Warnings = null
);
