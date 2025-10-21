namespace Logic.DeviceInterfaces
{
    public interface IActuatorDevice
    {
        public string ActuatorName { get; }

        public void Actuate(object state);
    }
}
