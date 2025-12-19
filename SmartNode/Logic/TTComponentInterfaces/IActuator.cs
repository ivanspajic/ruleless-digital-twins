namespace Logic.TTComponentInterfaces
{
    public interface IActuator
    {
        public string ActuatorName { get; }

        public void Actuate(object state);
    }
}
