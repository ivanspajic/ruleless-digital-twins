namespace Models
{
    public class Plan
    {
        public required IReadOnlyCollection<Action> Actions { get; init; }

        public TimeSpan MaximumExecutionDuration { get; init; }
    }
}
