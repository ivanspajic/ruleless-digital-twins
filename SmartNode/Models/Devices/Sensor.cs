using Models.Properties;
using System.Numerics;

namespace Models.Devices
{
    public class Sensor<T> where T : INumber<T>
    {
        public required string Name { get; init; }

        public required ObservableProperty<T> ObservableProperty { get; init; }
    }
}
