using System.Numerics;

namespace Models.Properties
{
    public class ObservableProperty<T> : Property where T : INumber<T>
    {
        public required T Value { get; init; }
    }
}
