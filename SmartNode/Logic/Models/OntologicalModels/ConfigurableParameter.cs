namespace Logic.Models.OntologicalModels
{
    internal class ConfigurableParameter : Property
    {
        public required object LowerLimitValue { get; init; }

        public required object UpperLimitValue { get; init; }
    }
}
