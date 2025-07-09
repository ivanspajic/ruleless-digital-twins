using Logic.Models.OntologicalModels;

namespace Logic.Models.MapekModels
{
    internal class PropertyCache
    {
        public required IDictionary<string, Property> Properties { get; init; }

        public required IDictionary<string, ConfigurableParameter> ConfigurableParameters { get; init; }
    }
}
