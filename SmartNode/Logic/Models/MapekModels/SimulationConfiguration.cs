using Logic.Models.OntologicalModels;

namespace Logic.Models.MapekModels
{
    internal class SimulationConfiguration
    {
        public IEnumerable<IEnumerable<ActuationAction>> TickActions { get; init; }

        public IEnumerable<ReconfigurationAction> PostTickActions { get; init; }
    }
}
