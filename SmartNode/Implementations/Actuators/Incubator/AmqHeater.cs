using Implementations.SimulatedTwinningTargets;
using Logic.TTComponentInterfaces;
using System.Diagnostics;

namespace Implementations.Actuators.Incubator {
    public class AMQHeater() : IActuator {
        private readonly IncubatorAdapter _incubatorAdapter = IncubatorAdapter.GetInstance("localhost", new CancellationToken());

        public string ActuatorName => "Incubator Heater";

        public void Actuate(object state) {
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
