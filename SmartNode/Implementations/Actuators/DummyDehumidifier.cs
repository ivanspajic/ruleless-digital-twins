using Implementations.SimulatedTwinningTargets;
using Logic.DeviceInterfaces;

namespace Implementations.Actuators
{
    public class DummyDehumidifier : IActuatorDevice
    {
        private int _actuatorState = 0;
        private readonly DummyRoomM370 _dummyRoomM370;

        public DummyDehumidifier(string actuatorName, DummyRoomM370 dummyRoomM370)
        {
            ActuatorName = actuatorName;
            _dummyRoomM370 = dummyRoomM370;
        }

        public string ActuatorName { get; }

        public void Actuate(object state)
        {
            _actuatorState = (int)state;

            // The dummy Actuator doesn't represent the differential equations found in the
            // respective FMU. These states are simplifications adjusted for 900s of actuation.
            if (_actuatorState == 1)
            {
                _dummyRoomM370.RoomHumidity -= 10;
            }
            else
            {
                // Simply touch the property to activate the "humidifying" mechanism in the dummy environment.
                _dummyRoomM370.RoomHumidity -= 0;
            }
        }
    }
}
