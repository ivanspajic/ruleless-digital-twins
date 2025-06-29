using Logic.DeviceInterfaces;

namespace SensorActuatorImplementations.Actuators
{
    public class ExampleActuator : IActuatorDevice
    {
        public string ActuatorName { get; init; }

        public void Actuate(string state)
        {
            // Simulates an actuation. For example, this could contain a procedure to connect to a Bluetooth device
            // or similar.
        }
    }
}
