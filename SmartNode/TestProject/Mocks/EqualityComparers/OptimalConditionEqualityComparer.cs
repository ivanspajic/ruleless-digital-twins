using Logic.Models.OntologicalModels;
using System.Diagnostics.CodeAnalysis;

namespace TestProject.Mocks.EqualityComparers
{
    internal class OptimalConditionEqualityComparer : IEqualityComparer<OptimalCondition>
    {
        private ConstraintExpressionEqualityComparer _constraintExpressionEqualityComparer = new ConstraintExpressionEqualityComparer();

        public bool Equals(OptimalCondition? x, OptimalCondition? y)
        {
            return x!.ReachedInMaximumSeconds == y!.ReachedInMaximumSeconds &&
                x.Name.Equals(y.Name) &&
                x.Property.Equals(y.Property) &&
                _constraintExpressionEqualityComparer.Equals(x.ConditionConstraint, y.ConditionConstraint);
        }

        public int GetHashCode([DisallowNull] OptimalCondition obj)
        {
            return obj.Name.GetHashCode() * 
                obj.ReachedInMaximumSeconds.GetHashCode() * 
                obj.Property.GetHashCode() *
                obj.ConditionConstraint.GetHashCode();
        }
    }
}
