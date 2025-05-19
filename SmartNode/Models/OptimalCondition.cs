namespace Models
{
    public class OptimalCondition : NamedIndividual
    {
        public required string Property { get; init; }

        public required IReadOnlyCollection<Tuple<ConstraintOperator, string>> Constraints { get; init; }

        public int ReachedInMaximumSeconds { get; init; }

        public required string ConstraintValueType { get; init; }
    }
}
