namespace Models
{
    public class ReconfigurationExecutionPlan : ExecutionPlan
    {
        public required ConfigurableParameter ConfigurableParameter { get; init; }

        public required Effect Effect { get; init; }
    }
}
