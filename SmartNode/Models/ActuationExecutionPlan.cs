namespace Models
{
    public class ActuationExecutionPlan : ExecutionPlan
    {
        public required ActuatorState ActuatorState { get; init; }
    }
}
