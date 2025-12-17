using Logic.DeviceInterfaces;
using Logic.Models.OntologicalModels;

namespace Logic.Models.MapekModels
{
    public class PropertyCache
    {
        public required IDictionary<string, Property> Properties { get; init; }

        public required IDictionary<string, ConfigurableParameter> ConfigurableParameters { get; init; }
    }
}
