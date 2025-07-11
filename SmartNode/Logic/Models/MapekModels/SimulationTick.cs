using Logic.Models.OntologicalModels;

namespace Logic.Models.MapekModels
{
    internal class SimulationTick
    {
        public int TickIndex { get; init; }

        public required IEnumerable<ActuationAction> ActionsToExecute { get; init; }
    }
}
