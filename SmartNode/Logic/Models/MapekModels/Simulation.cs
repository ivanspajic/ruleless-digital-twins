using Logic.Models.OntologicalModels;

namespace Logic.Models.MapekModels
{
    public class Simulation
    {
        public Simulation(Cache propertyCache)
        {
            PropertyCache = propertyCache;
            ActuationActions = [];
            Index = -1;
            ReconfigurationActions = [];
        }

        public int Index { get; init; }

        public IEnumerable<ActuationAction> ActuationActions { get; init; }

        public IEnumerable<ReconfigurationAction> ReconfigurationActions { get; init; }

        public Cache? PropertyCache { get; set; }
    }
}
