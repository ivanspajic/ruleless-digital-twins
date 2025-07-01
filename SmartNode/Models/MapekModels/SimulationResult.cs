using Models.OntologicalModels;

namespace Models.MapekModels
{
    public class SimulationResult
    {
        public required IEnumerable<Action> Actions { get; init; }

        public bool OptimalConditionsSatisfied { get; init; }

        public required IEnumerable<Property> OptimizedProperties { get; init; }
    }
}
