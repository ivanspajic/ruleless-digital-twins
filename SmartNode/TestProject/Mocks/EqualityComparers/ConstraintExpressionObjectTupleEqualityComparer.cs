using Logic.Models.MapekModels;
using System.Diagnostics.CodeAnalysis;

namespace TestProject.Mocks.EqualityComparers
{
    internal class ConstraintExpressionObjectTupleEqualityComparer : IEqualityComparer<(ConstraintExpression, object)>
    {
        private ConstraintExpressionEqualityComparer _constraintExpressionEqualityComparer = new ConstraintExpressionEqualityComparer();

        public bool Equals((ConstraintExpression, object) x, (ConstraintExpression, object) y)
        {
            if (_constraintExpressionEqualityComparer.Equals(x.Item1, y.Item1) && x.Item2.Equals(y.Item2))
            {
                return true;
            }

            return false;
        }

        public int GetHashCode([DisallowNull] (ConstraintExpression, object) obj)
        {
            return _constraintExpressionEqualityComparer.GetHashCode(obj.Item1) * obj.Item2.GetHashCode();
        }
    }
}
