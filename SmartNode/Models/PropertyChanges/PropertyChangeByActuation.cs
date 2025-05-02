using Models.Devices;

namespace Models.PropertyChanges
{
    public class PropertyChangeByActuation : PropertyChange
    {
        public required Actuator Actuator { get; init; }
    }
}
