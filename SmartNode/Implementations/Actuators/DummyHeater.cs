using Implementations.SimulatedTwinningTargets;
using Logic.DeviceInterfaces;
using System.Globalization;

namespace Implementations.Actuators
{
    public class DummyHeater : IActuatorDevice
    {
        private int _actuatorState = 0;
        private readonly DummyRoomM370 _dummyRoomM370;

        public DummyHeater(string actuatorName, DummyRoomM370 dummyRoomM370)
        {
            ActuatorName = actuatorName;
            _dummyRoomM370 = dummyRoomM370;
        }

        public string ActuatorName { get; }

        public void Actuate(object state)
        {
            if (state is not int) {
                state = int.Parse(state.ToString()!, CultureInfo.InvariantCulture);
            }
            _actuatorState = (int)state;

            // The dummy Actuator doesn't represent the differential equations found in the
            // respective FMU. These states are simplifications adjusted for 900s of actuation.            
            _dummyRoomM370.RoomTemperature += _actuatorState switch {
                2 => 10,
                1 => 5,
                _ => 0 // Simply touch the property to activate the "cooling" mechanism in the dummy environment.
            };
        }
    }
}
