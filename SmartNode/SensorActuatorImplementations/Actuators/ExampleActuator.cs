using Logic.DeviceInterfaces;

namespace Implementations.Actuators
{
    public class ExampleActuator : IActuatorDevice
    {
        public required string ActuatorName { get; init; }

        public void Actuate(object state, double durationSeconds)
        {
            // Simulates an actuation. For example, this could contain a procedure to connect to a Bluetooth device or similar.
        }
    }
}
