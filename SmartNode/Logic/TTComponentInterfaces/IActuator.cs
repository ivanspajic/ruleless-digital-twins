namespace Logic.TTComponentInterfaces
{
    public interface IActuator
    {
        public string ActuatorName { get; }

        public Task Actuate(object state);
    }
}
