namespace Logic.Models.MapekModels
{
    public class AtomicConstraintExpression : ConstraintExpression
    {
        public required object Left { get; init; }

        public required object Right { get; init; }
    }
}
