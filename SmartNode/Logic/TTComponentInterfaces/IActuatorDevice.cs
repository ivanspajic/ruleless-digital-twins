namespace Logic.TTComponentInterfaces
{
    public interface IActuatorDevice
    {
        public string ActuatorName { get; }

        public void Actuate(object state);
    }
}
