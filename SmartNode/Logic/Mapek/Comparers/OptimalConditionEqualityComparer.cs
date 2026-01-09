using Logic.Models.OntologicalModels;
using System.Diagnostics.CodeAnalysis;

namespace Logic.Mapek.Comparers {
    internal class OptimalConditionEqualityComparer : IEqualityComparer<OptimalCondition> {
        public bool Equals(OptimalCondition? x, OptimalCondition? y) {
            return x!.Name.Equals(y!.Name) &&
                x.Property.Equals(y.Property) &&
                x.ConstraintValueType.Equals(y.ConstraintValueType) &&
                x.ReachedInMaximumSeconds == y.ReachedInMaximumSeconds &&
                x.UnsatisfiedAtomicConstraints.SequenceEqual(y.UnsatisfiedAtomicConstraints, new ConstraintExpressionEqualityComparer()) &&
                x.Constraints.SequenceEqual(y.Constraints, new ConstraintExpressionEqualityComparer());
        }

        public int GetHashCode([DisallowNull] OptimalCondition obj) {
            return obj.GetHashCode() *
                obj.Property.GetHashCode() *
                obj.ConstraintValueType.GetHashCode() *
                obj.ReachedInMaximumSeconds.GetHashCode() *
                obj.UnsatisfiedAtomicConstraints.GetHashCode() *
                obj.Constraints.GetHashCode() *
                obj.Name.GetHashCode();
        }
    }
}
