namespace Models.OntologicalModels
{
    public class OptimalCondition : NamedIndividual
    {
        public required string Property { get; init; }

        public required IEnumerable<Tuple<ConstraintOperator, string>> Constraints { get; init; }

        public int ReachedInMaximumSeconds { get; init; }

        public required string ConstraintValueType { get; init; }
    }
}
