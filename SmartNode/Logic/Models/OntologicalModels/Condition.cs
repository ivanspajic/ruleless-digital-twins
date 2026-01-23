using Logic.Models.MapekModels;

namespace Logic.Models.OntologicalModels
{
    public class Condition : NamedIndividual
    {
        public required Property Property { get; init; }

        public required IEnumerable<ConstraintExpression> Constraints { get; init; }

        public int ReachedInMaximumSeconds { get; init; }

        public int Priority { get; init; }

        public bool IsOptimalCondition { get; init; }

        public DateTime SatisfiedBy { get; init; }
    }
}
