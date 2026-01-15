using Implementations.SimulatedTwinningTargets;
using Logic.TTComponentInterfaces;
using System.Diagnostics;

namespace Implementations.Actuators.Incubator {
    public class AmqHeater(IncubatorAdapter incubatorAdapter) : IActuator {
        private readonly IncubatorAdapter _incubatorAdapter = incubatorAdapter;

        public string ActuatorName => "Incubator Heater";

        public async Task Actuate(object state) {
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
