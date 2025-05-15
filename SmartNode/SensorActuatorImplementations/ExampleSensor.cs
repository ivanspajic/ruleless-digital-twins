using Logic.DeviceInterfaces;

namespace SensorActuatorImplementations
{
    public class ExampleSensor : ISensor
    {
        public required string SensorName { get; init; }

        public required string ProcedureName { get; init; }

        public object ObservePropertyValue(params object[] inputs)
        {
            // Test value.
            return 15.3;
        }
    }
}
