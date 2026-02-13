using Logic.Models.MapekModels;

namespace Logic.Models.OntologicalModels
{
    public class OptimalCondition : NamedIndividual
    {
        public required Property Property { get; init; }

        public required ConstraintExpression ConditionConstraint { get; init; }

        public ConstraintExpression? EnablingConstraint { get; init; }

        public int? ReachedInMaximumSeconds { get; init; }

        public int? Priority { get; init; }

        public DateTime? SatisfiedBy { get; init; }

        public bool IsBreakable { get; init; }
    }
}
