using Logic.Models.OntologicalModels;

namespace Logic.Models.MapekModels
{
    internal class SimulationConfiguration
    {
        public required IEnumerable<SimulationTick> SimulationTicks { get; init; }

        public required IEnumerable<ReconfigurationAction> PostTickActions { get; init; }

        public PropertyCache ResultingPropertyCache { get; set; }
    }
}
