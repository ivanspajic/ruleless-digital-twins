using Implementations.SimulatedTwinningTargets;
using Logic.TTComponentInterfaces;
using System.Globalization;

namespace Implementations.Actuators.RoomM370
{
    public class DummyDehumidifier : IActuator {
        private int _actuatorState = 0;
        private readonly DummyRoomM370 _dummyRoomM370;

        public DummyDehumidifier(string actuatorName) {
            ActuatorName = actuatorName;
            _dummyRoomM370 = DummyRoomM370.Instance;
        }

        public string ActuatorName { get; }

        public object ActuatorState {
            get {
                return _actuatorState;
            }
        }

        public async Task Actuate(object state) {
            if (state is not int) {
                state = int.Parse(state.ToString()!, CultureInfo.InvariantCulture);
            }
            _actuatorState = (int)state;

            _dummyRoomM370.DehumidifierState = _actuatorState;
        }

        public void RunDummyEnvironment(double mapekExecutionDurationSeconds) {
            _dummyRoomM370.ExecuteFmu(mapekExecutionDurationSeconds);
        }
    }
}
