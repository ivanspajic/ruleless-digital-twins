using Logic.Models.OntologicalModels;

namespace Logic.Models.MapekModels
{
    public class Simulation
    {
        public required int Index { get; init; }

        public required IEnumerable<ActuationAction> ActuationActions { get; init; }

        public required IEnumerable<ReconfigurationAction> ReconfigurationActions { get; init; }

        public PropertyCache? PropertyCache { get; set; }
    }
}
