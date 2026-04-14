using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using System.Diagnostics.CodeAnalysis;

namespace Logic.Mapek.Comparers {
    internal class OptimalConditionEqualityComparer : IEqualityComparer<OptimalCondition> {
        private readonly IEqualityComparer<Property> _propertyEqualityComparer;
        private readonly IEqualityComparer<ConstraintExpression> _constraintExpressionEqualityComparer = new ConstraintExpressionEqualityComparer();

        public OptimalConditionEqualityComparer(double fuzzinessFactor) {
            _propertyEqualityComparer = new FuzzyPropertyEqualityComparer(fuzzinessFactor);
        }

        public bool Equals(OptimalCondition? x, OptimalCondition? y) {
            return x!.Name.Equals(y!.Name) &&
                _propertyEqualityComparer.Equals(x!.Property, y!.Property) &&
                x.ReachedInMaximumSeconds == y.ReachedInMaximumSeconds &&
                _constraintExpressionEqualityComparer.Equals(x.ConditionConstraint, y.ConditionConstraint) &&
                _constraintExpressionEqualityComparer.Equals(x.EnablingConstraint, y.EnablingConstraint);
        }

        public int GetHashCode([DisallowNull] OptimalCondition obj) {
            var hashCode = 1;

            if (obj.EnablingConstraint is not null) {
                hashCode *= obj.EnablingConstraint.GetHashCode();
            }

            return hashCode *
                obj.GetHashCode() *
                obj.Property.GetHashCode() *
                obj.ReachedInMaximumSeconds.GetHashCode() *
                obj.ConditionConstraint.GetHashCode() *
                obj.Name.GetHashCode();
        }
    }
}
