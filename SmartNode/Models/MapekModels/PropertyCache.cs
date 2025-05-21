namespace Models.MapekModels
{
    public class PropertyCache
    {
        public required IDictionary<string, ObservableProperty> ObservableProperties { get; init; }

        public required IDictionary<string, InputOutput> ComputableProperties { get; init; }

        public required IDictionary<string, ConfigurableParameter> ConfigurableParameters { get; init; }
    }
}
