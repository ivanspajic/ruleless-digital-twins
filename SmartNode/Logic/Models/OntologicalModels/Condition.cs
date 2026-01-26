using Logic.Models.MapekModels;

namespace Logic.Models.OntologicalModels
{
    public class Condition : NamedIndividual
    {
        public required Property Property { get; init; }

        public required ConstraintExpression Constraint { get; init; }

        public int? ReachedInMaximumSeconds { get; init; }

        public int? Priority { get; init; }

        public DateTime? SatisfiedBy { get; init; }

        public bool IsBreakable { get; init; }
    }
}
