using Logic.Models.MapekModels.Serializables;
using Logic.Models.OntologicalModels;

namespace Logic.Models.MapekModels
{
    public class PropertyCache
    {
        public required IDictionary<string, Property> Properties { get; init; }

        public required IDictionary<string, ConfigurableParameter> ConfigurableParameters { get; init; }

        public SerializablePropertyCache SerializablePropertyCache {
            get {
                var serializableProperties = new List<SerializableProperty>();

                serializableProperties.AddRange(Properties.Values.Select(property => property.SerializableProperty));
                serializableProperties.AddRange(ConfigurableParameters.Values.Select(property => property.SerializableProperty));

                return new SerializablePropertyCache {
                    Properties = serializableProperties
                };
            }
        }
    }
}
