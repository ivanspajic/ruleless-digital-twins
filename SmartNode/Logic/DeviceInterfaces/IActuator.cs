namespace Logic.DeviceInterfaces
{
    public interface IActuator
    {
        public string ActuatorName { get; init; }

        /// <summary>
        /// Performs an actuation.
        /// </summary>
        /// <param name="state">The state of the actuator.</param>
        /// 
        /// This setup could be further considered. Actuations might also need to return some information.
        public void Actuate(string state);
    }
}
