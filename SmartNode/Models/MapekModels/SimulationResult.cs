namespace Models.MapekModels
{
    public class SimulationResult
    {
        public required List<Models.Action> Actions { get; init; }

        public bool OptimalConditionsSatisfied { get; init; }

        public required IReadOnlyCollection<Property> OptimizedProperties { get; init; }
    }
}
