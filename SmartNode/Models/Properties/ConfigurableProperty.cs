using System.Numerics;

namespace Models.Properties
{
    public class ConfigurableProperty<T> where T : INumber<T>
    {
        public required string Name { get; init; }

        public required T Value { get; set; }
    }
}
