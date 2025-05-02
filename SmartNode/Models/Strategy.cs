using Models.Executions;

namespace Models
{
    public class Strategy
    {
        public required IReadOnlyList<Execution> Executions { get; init; }

        public TimeSpan TimeLimit { get; init; }

        public required string Identifier { get; init; }
    }
}
