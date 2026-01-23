using Logic.Models.OntologicalModels;
using System.Diagnostics.CodeAnalysis;

namespace TestProject.Mocks.EqualityComparers
{
    internal class ConditionEqualityComparer : IEqualityComparer<Condition>
    {
        private ConstraintExpressionEqualityComparer _constraintExpressionEqualityComparer = new ConstraintExpressionEqualityComparer();

        public bool Equals(Condition? x, Condition? y)
        {
            if (x.ReachedInMaximumSeconds == y.ReachedInMaximumSeconds &&
                x.Name.Equals(y.Name) &&
                x.Property.Equals(y.Property))
            {
                foreach (var xConstraint in x.Constraints)
                {
                    if (!y.Constraints.Contains(xConstraint, _constraintExpressionEqualityComparer))
                    {
                        return false;
                    }
                }

                foreach (var yConstraint in y.Constraints)
                {
                    if (!x.Constraints.Contains(yConstraint, _constraintExpressionEqualityComparer))
                    {
                        return false;
                    }
                }

                return true;
            }

            return false;
        }

        public int GetHashCode([DisallowNull] Condition obj)
        {
            var hashCode = obj.Name.GetHashCode() * 
                obj.ReachedInMaximumSeconds.GetHashCode() * 
                obj.Property.GetHashCode();

            foreach (var constraint in obj.Constraints)
            {
                hashCode *= _constraintExpressionEqualityComparer.GetHashCode(constraint);
            }

            return hashCode;
        }
    }
}
