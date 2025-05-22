namespace Models
{
    public class ActuationExecutionPlan : NamedIndividual
    {
        public required ActuatorState ActuatorState { get; init; }
    }
}
