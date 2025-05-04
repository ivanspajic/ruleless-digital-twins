using System.Numerics;

namespace Models.Properties
{
    public class ObservableProperty<T> where T : INumber<T>
    {
        public required string Name { get; init; }

        public required T Value { get; set; }
    }
}
