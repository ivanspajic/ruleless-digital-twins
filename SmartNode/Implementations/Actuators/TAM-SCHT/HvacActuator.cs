using Logic.TTComponentInterfaces;
using System.Globalization;

namespace Implementations.Actuators.TAM_SCHT {
    public class HvacActuator : IActuator {
        private int _actuatorState = 0;

        public HvacActuator(string actuatorName) {
            ActuatorName = actuatorName;
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
        }
    }
}
