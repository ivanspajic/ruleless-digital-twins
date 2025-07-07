namespace Logic.Models.MapekModels
{
    public class NestedConstraintExpression : ConstraintExpression
    {
        public required ConstraintExpression Left { get; init; }

        public required ConstraintExpression Right { get; init; }
    }
}
