namespace Logic.DeviceInterfaces
{
    public interface IActuator
    {
        public string Name { get; init; }

        /// <summary>
        /// Performs an actuation.
        /// </summary>
        /// <param name="state">The state of the actuator.</param>
        /// <param name="stateValue">The optional state value.</param>
        public void Actuate(int state, int? stateValue = null); // TODO: consider this setup further. Actuations might return some information.
    }
}
