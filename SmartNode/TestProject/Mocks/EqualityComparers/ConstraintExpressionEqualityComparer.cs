using Logic.Models.MapekModels;
using System.Diagnostics.CodeAnalysis;

namespace TestProject.Mocks.EqualityComparers
{
    internal class ConstraintExpressionEqualityComparer : IEqualityComparer<ConstraintExpression>
    {
        public bool Equals(ConstraintExpression? x, ConstraintExpression? y)
        {
            if (x is AtomicConstraintExpression && y is AtomicConstraintExpression)
            {
                var xAtomic = x as AtomicConstraintExpression;
                var yAtomic = y as AtomicConstraintExpression;

                if (xAtomic!.ConstraintType == yAtomic!.ConstraintType && xAtomic!.Right.Equals(yAtomic!.Right))
                {
                    return true;
                }
            }
            else if (x is NestedConstraintExpression && y is NestedConstraintExpression)
            {
                var xNested = x as NestedConstraintExpression;
                var yNested = y as NestedConstraintExpression;

                if (xNested!.ConstraintType == yNested!.ConstraintType &&
                    Equals(xNested!.Right, yNested!.Right) &&
                    Equals(xNested!.Left, yNested!.Left))
                {
                    return true;
                }
            }

            return false;
        }

        public int GetHashCode([DisallowNull] ConstraintExpression obj)
        {
            if (obj is AtomicConstraintExpression)
            {
                var objAtomic = obj as AtomicConstraintExpression;

                return objAtomic!.ConstraintType.GetHashCode() * objAtomic!.Right.GetHashCode();
            }
            else
            {
                var objNested = obj as NestedConstraintExpression;

                return GetHashCode(objNested!.Left) * GetHashCode(objNested!.Right) * objNested.ConstraintType.GetHashCode();
            }
        }
    }
}
