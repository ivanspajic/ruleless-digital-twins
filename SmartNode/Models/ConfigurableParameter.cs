namespace Models
{
    public class ConfigurableParameter : Property
    {
        public required object ValueIncrements { get; init; }

        public required object LowerLimitValue { get; init; }

        public required object UpperLimitValue { get; init; }
    }
}
