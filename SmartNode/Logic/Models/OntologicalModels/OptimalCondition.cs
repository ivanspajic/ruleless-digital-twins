using Logic.Models.MapekModels;

namespace Logic.Models.OntologicalModels
{
    public class OptimalCondition : NamedIndividual
    {
        public required string Property { get; init; }

        public required IEnumerable<ConstraintExpression> Constraints { get; init; }

        public IEnumerable<AtomicConstraintExpression> UnsatisfiedAtomicConstraints { get; set; }

        public int ReachedInMaximumSeconds { get; init; }

        public required string ConstraintValueType { get; init; }
    }
}
