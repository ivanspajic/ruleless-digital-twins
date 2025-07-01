using Models.OntologicalModels;

namespace Models.MapekModels
{
    public class PropertyCache
    {
        public required IDictionary<string, Property> Properties { get; init; }

        public required IDictionary<string, ConfigurableParameter> ConfigurableParameters { get; init; }
    }
}
