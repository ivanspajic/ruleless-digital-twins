using Models.Properties;
using System.Numerics;

namespace Models.PropertyChanges
{
    public class PropertyChangeByReconfiguration<T> : PropertyChange where T : INumber<T>
    {
        public Effect AlteredByEffect { get; init; }

        public required ConfigurableProperty<T> ConfigurableProperty { get; init; }
    }
}
