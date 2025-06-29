namespace Logic.DeviceInterfaces
{
    public interface IActuatorDevice
    {
        public string ActuatorName { get; init; }

        /// <summary>
        /// Performs an actuation.
        /// </summary>
        /// <param name="state">The state of the actuator.</param>
        /// 
        /// This setup could be further considered to include overloads with a time duration
        /// or other kinds of parameters. The Actuator could also return some information.
        public void Actuate(string state);
    }
}
