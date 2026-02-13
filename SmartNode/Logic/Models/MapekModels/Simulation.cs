using Logic.Models.MapekModels.Serializables;
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

        public SerializableSimulation SerializableSimulation => new() {
            Index = Index,
            Actions = Actions.Select(action =>
                action is ActuationAction actuationAction
                    ? actuationAction.SerializableAction
                    : ((ReconfigurationAction)action).SerializableAction),
            PropertyCache = PropertyCache.SerializablePropertyCache
        };
    }
}
