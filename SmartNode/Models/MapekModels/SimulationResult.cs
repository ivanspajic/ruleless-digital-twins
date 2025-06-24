namespace Models.MapekModels
{
    public class SimulationResult
    {
        public required string ActionName { get; init; }

        public bool OptimalConditionsSatisfied { get; init; }

        public required IReadOnlyCollection<Property> OptimizedProperties { get; init; }
    }
}
