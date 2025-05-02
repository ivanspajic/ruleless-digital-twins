using System.Numerics;

namespace Models.Properties
{
    public class ConfigurableProperty<T> : Property where T : INumber<T>
    {
        public required T Value { get; init; }
    }
}
