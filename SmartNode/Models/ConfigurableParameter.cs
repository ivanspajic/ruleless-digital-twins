namespace Models
{
    public class ConfigurableParameter : NamedIndividual
    {
        public required object Value { get; init; }

        public required object ValueIncrement { get; init; }

        public required object LowerLimitValue { get; init; }

        public required object UpperLimitValue { get; init; }
    }
}
