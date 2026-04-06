using Implementations.SimulatedTwinningTargets;
using Logic.TTComponentInterfaces;
using System.Diagnostics;

namespace Implementations.Actuators.Incubator {
    public class AmqHeater(IncubatorAdapter incubatorAdapter) : IActuator {
        private const string DefaultState = "0";
        private object _currentState = null!;

        private readonly IncubatorAdapter _incubatorAdapter = incubatorAdapter;

        public string ActuatorName => "Incubator Heater";

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

            var _actuatorState = int.Parse((string)state);
            if (_actuatorState == 0) {
                Task t = Task.Run(async () => await _incubatorAdapter.SetHeater(false));
            } else if (_actuatorState == 1) {
                Task t = Task.Run(async () => await _incubatorAdapter.SetHeater(true));
                t.Wait();
            } else {
                Debug.Fail($"Unexpected value {_actuatorState}!");
            }
        }
    }
}
