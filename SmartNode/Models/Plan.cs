namespace Models
{
    public class Plan
    {
        public required IReadOnlyCollection<Action> Actions { get; init; }
    }
}
