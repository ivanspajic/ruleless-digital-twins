using Logic.Models.OntologicalModels;

namespace Logic.Models.MapekModels
{
    public class Simulation
    {
        public Simulation(PropertyCache propertyCache)
        {
            PropertyCache = propertyCache;
            InitializationActions = [];
            Actions = [];
            Index = -1;
        }

        public int Index { get; init; }

        public IEnumerable<OntologicalModels.Action> Actions { get; init; }

        public PropertyCache PropertyCache { get; set; }

        public IEnumerable<FMUParameterAction> InitializationActions { get; set; }
    }
}
