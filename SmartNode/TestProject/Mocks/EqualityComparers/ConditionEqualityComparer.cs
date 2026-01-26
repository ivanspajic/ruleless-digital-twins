using Logic.Models.OntologicalModels;
using System.Diagnostics.CodeAnalysis;

namespace TestProject.Mocks.EqualityComparers
{
    internal class ConditionEqualityComparer : IEqualityComparer<Condition>
    {
        private ConstraintExpressionEqualityComparer _constraintExpressionEqualityComparer = new ConstraintExpressionEqualityComparer();

        public bool Equals(Condition? x, Condition? y)
        {
            return x!.ReachedInMaximumSeconds == y!.ReachedInMaximumSeconds &&
                x.Name.Equals(y.Name) &&
                x.Property.Equals(y.Property) &&
                _constraintExpressionEqualityComparer.Equals(x.Constraint, y.Constraint);
        }

        public int GetHashCode([DisallowNull] Condition obj)
        {
            return obj.Name.GetHashCode() * 
                obj.ReachedInMaximumSeconds.GetHashCode() * 
                obj.Property.GetHashCode() *
                obj.Constraint.GetHashCode();
        }
    }
}
