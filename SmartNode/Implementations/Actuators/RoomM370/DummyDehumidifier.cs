using Implementations.SimulatedTwinningTargets;
using Logic.TTComponentInterfaces;
using System.Globalization;

namespace Implementations.Actuators.RoomM370
{
    public class DummyDehumidifier : IActuator
    {
        private int _actuatorState = 0;
        private readonly DummyRoomM370 _dummyRoomM370;

        public DummyDehumidifier(string actuatorName)
        {
            ActuatorName = actuatorName;
            _dummyRoomM370 = DummyRoomM370.Instance;
        }

        public string ActuatorName { get; }

        public async Task Actuate(object state)
        {
            if (state is not int) {
                state = int.Parse(state.ToString()!, CultureInfo.InvariantCulture);
            }
            _actuatorState = (int)state;

            // The dummy Actuator doesn't represent the differential equations found in the
            // respective FMU. These states are simplifications adjusted for 900s of actuation.
            _dummyRoomM370.RoomHumidity -= _actuatorState switch {
                1 => 10,
                _ => 0 // Simply touch the property to activate the "cooling" mechanism in the dummy environment.
            };
        }
    }
}
