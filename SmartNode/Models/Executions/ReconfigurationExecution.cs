using Models.Properties;

namespace Models.Executions
{
    public class ReconfigurationExecution : Execution
    {
        public required Property Property { get; init; }

        public Effect AffectedWithEffect { get; init; }
    }
}
