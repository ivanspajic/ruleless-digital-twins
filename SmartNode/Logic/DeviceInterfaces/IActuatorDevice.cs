namespace Logic.DeviceInterfaces
{
    public interface IActuatorDevice
    {
        public string ActuatorName { get; init; }

        public void Actuate(object state, double durationSeconds);
    }
}
