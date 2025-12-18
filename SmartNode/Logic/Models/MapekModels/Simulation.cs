using Logic.Models.OntologicalModels;

namespace Logic.Models.MapekModels
{
    public class Simulation
    {
        public Simulation(PropertyCache propertyCache)
        {
            PropertyCache = propertyCache;
            InitializationActions = [];
            ActuationActions = [];
            Index = -1;
            ReconfigurationActions = [];
        }

        public int Index { get; init; }

        public IEnumerable<ActuationAction> ActuationActions { get; init; }

        public IEnumerable<ReconfigurationAction> ReconfigurationActions { get; init; }

        public PropertyCache? PropertyCache { get; set; }
        public IEnumerable<ActuationAction> InitializationActions { get; set; }
    }
}
