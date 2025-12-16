using Logic.DeviceInterfaces;
using Logic.Models.OntologicalModels;

namespace Logic.Models.MapekModels
{
    public class Cache
    {
        public required IDictionary<string, Property> Properties { get; init; }

        public required IDictionary<string, ConfigurableParameter> ConfigurableParameters { get; init; }

        public required SoftSensorTreeNode SoftSensorTree { get; set; }
    }
}
