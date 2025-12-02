using Logic.Models.OntologicalModels;

namespace Logic.Models.MapekModels
{
    public class SimulationTick
    {
        public required int TickIndex { get; init; }

        public required IEnumerable<ActuationAction> ActuationActions { get; init; }

        public required IEnumerable<ReconfigurationAction> ReconfigurationActions { get; init; }
    }
}
