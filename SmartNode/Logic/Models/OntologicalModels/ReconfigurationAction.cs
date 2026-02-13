using Logic.Mapek;
using Logic.Models.MapekModels.Serializables;

namespace Logic.Models.OntologicalModels
{
    public class ReconfigurationAction : Action
    {
        public required ConfigurableParameter ConfigurableParameter { get; init; }

        // This isn't required on instantiation as the reconfiguration value isn't known until
        // the Plan phase has been reached.
        public object NewParameterValue { get; set; }

        public SerializableAction SerializableAction => new() {
            Actuator = ConfigurableParameter.Name.GetSimpleName(),
            Value = NewParameterValue
        };
    }
}
