namespace Models.MapekModels
{
    public class SimulationResult<T> where T : Action
    {
        public required List<T> Actions { get; init; }

        public bool OptimalConditionsSatisfied { get; init; }

        public required IReadOnlyCollection<Property> OptimizedProperties { get; init; }
    }
}
