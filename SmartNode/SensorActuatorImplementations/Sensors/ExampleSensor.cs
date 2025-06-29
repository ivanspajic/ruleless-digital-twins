using Logic.DeviceInterfaces;

namespace SensorActuatorImplementations.Sensors
{
    public class ExampleSensor : ISensorDevice
    {
        public required string SensorName { get; init; }

        public required string ProcedureName { get; init; }

        public object ObservePropertyValue(params object[] inputs)
        {
            var random = new Random();

            // Return a fake calculation as a bare minimum.
            //return random.NextDouble() * 15;
            return 1.02;
        }
    }
}
