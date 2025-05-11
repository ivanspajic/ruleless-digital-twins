using Logic.DeviceInterfaces;

namespace SensorActuatorImplementations
{
    public class ExampleSensor : ISensor
    {
        public required string Name { get; init; }

        public object ObservePropertyValue(params object[] inputs)
        {
            // Test value.
            return 15.3;
        }
    }
}
