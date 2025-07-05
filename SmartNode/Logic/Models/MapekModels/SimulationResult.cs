using Logic.Models.OntologicalModels;

namespace Logic.Models.MapekModels
{
    public class SimulationResult
    {
        public required IEnumerable<OntologicalModels.Action> Actions { get; init; }

        public bool OptimalConditionsSatisfied { get; init; }

        public required IEnumerable<Property> OptimizedProperties { get; init; }
    }
}
