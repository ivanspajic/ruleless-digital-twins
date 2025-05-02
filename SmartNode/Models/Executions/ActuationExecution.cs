using Models.Devices;

namespace Models.Executions
{
    public class ActuationExecution : Execution
    {
        public required Actuator Actuator { get; init; }
    }
}
