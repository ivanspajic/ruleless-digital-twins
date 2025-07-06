using System.Linq.Expressions;

namespace Logic.Models.OntologicalModels
{
    public class OptimalCondition : NamedIndividual
    {
        public required string Property { get; init; }

        public required IEnumerable<BinaryExpression> Constraints { get; init; }

        public IEnumerable<BinaryExpression> UnsatisfiedAtomicConstraints { get; set; }

        public int ReachedInMaximumSeconds { get; init; }

        public required string ConstraintValueType { get; init; }
    }
}
