using System.Linq.Expressions;

namespace Logic.Models.OntologicalModels
{
    public class OptimalCondition : NamedIndividual
    {
        public required string Property { get; init; }

        public required BinaryExpression ConstraintExpression { get; init; }

        public int ReachedInMaximumSeconds { get; init; }

        public required string ConstraintValueType { get; init; }
    }
}
