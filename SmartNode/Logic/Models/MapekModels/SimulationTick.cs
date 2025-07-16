using Logic.Models.OntologicalModels;

namespace Logic.Models.MapekModels
{
    internal class SimulationTick
    {
        public required int TickIndex { get; init; }

        public required double TickDurationSeconds { get; init; }

        public required IEnumerable<ActuationAction> ActionsToExecute { get; init; }
    }
}
