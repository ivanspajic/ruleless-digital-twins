using Logic.Models.OntologicalModels;

namespace Logic.Models.MapekModels
{
    public class AtomicConstraintExpression : ConstraintExpression
    {
        public required Property Property { get; init; }
    }
}
