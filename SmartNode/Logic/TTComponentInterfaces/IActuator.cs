namespace Logic.TTComponentInterfaces
{
    public interface IActuator
    {
        public string ActuatorName { get; }

        public object ActuatorState { get; }

        public Task Actuate(object state);

        // This workaround lets us actuate the dummy environment after all actuations have been set without passing in the environment
        // directly to MapekExecute.
        public void RunDummyEnvironment(double mapekExecutionDurationSeconds);
    }
}
