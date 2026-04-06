using Implementations.SimulatedTwinningTargets;
using Logic.TTComponentInterfaces;

namespace Implementations.Actuators.RoomM370 {
    public class DummyFloorHeating : IActuator {
        private const string DefaultState = "0";
        private object _currentState = null!;

        private int _actuatorState = 0;
        private readonly DummyRoomM370 _dummyRoomM370;

        public DummyFloorHeating(string actuatorName, DummyRoomM370 dummyRoomM370) {
            ActuatorName = actuatorName;
            _dummyRoomM370 = dummyRoomM370;
        }

        public string ActuatorName { get; }

        public object ActuatorState {
            get {
                return _currentState ?? DefaultState;
            }
            private set {
                _currentState = value;
            }
        }

        public async Task Actuate(object state) {
            ActuatorState = state;

            _actuatorState = (int)state;

            // The dummy Actuator doesn't represent the differential equations found in the
            // respective FMU. These states are simplifications adjusted for 900s of actuation.            
            _dummyRoomM370.RoomTemperature += _actuatorState switch {
                1 => 7,
                _ => 0 // Simply touch the property to activate the "cooling" mechanism in the dummy environment.
            };
        }
    }
}
