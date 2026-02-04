using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using System.Diagnostics.CodeAnalysis;

namespace Logic.Mapek.Comparers {
    internal class OptimalConditionEqualityComparer : IEqualityComparer<OptimalCondition> {
        private readonly IEqualityComparer<Property> _propertyEqualityComparer = new PropertyEqualityComparer();
        private readonly IEqualityComparer<ConstraintExpression> _constraintExpressionEqualityComparer = new ConstraintExpressionEqualityComparer();

        public bool Equals(OptimalCondition? x, OptimalCondition? y) {
            return x!.Name.Equals(y!.Name) &&
                _propertyEqualityComparer.Equals(x!.Property, y!.Property) &&
                x.ReachedInMaximumSeconds == y.ReachedInMaximumSeconds &&
                _constraintExpressionEqualityComparer.Equals(x.Constraint, y.Constraint);
        }

        public int GetHashCode([DisallowNull] OptimalCondition obj) {
            return obj.GetHashCode() *
                obj.Property.GetHashCode() *
                obj.ReachedInMaximumSeconds.GetHashCode() *
                obj.Constraint.GetHashCode() *
                obj.Name.GetHashCode();
        }
    }
}
