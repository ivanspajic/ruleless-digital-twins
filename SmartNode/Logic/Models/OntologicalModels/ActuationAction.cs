using Logic.Mapek;
using Logic.Models.MapekModels.Serializables;

namespace Logic.Models.OntologicalModels
{
    public class ActuationAction : Action
    {
        public required Actuator Actuator { get; init; }

        // This isn't required on instantiation as the reconfiguration value isn't known until
        // the Plan phase has been reached.
        public object NewStateValue { get; set; }

        public SerializableAction SerializableAction => new() {
            Actuator = Actuator.Name.GetSimpleName(),
            Value = NewStateValue
        };
    }
}
