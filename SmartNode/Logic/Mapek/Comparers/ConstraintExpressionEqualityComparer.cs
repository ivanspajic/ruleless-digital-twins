using Logic.Models.MapekModels;
using System.Diagnostics.CodeAnalysis;

namespace Logic.Mapek.Comparers {
    internal class ConstraintExpressionEqualityComparer : IEqualityComparer<ConstraintExpression> {
        public bool Equals(ConstraintExpression? x, ConstraintExpression? y) {
            if (x is NestedConstraintExpression nestedX && y is NestedConstraintExpression nestedY) {
                return nestedX.ConstraintType == nestedY.ConstraintType &&
                    Equals(nestedX.Left, nestedY.Left);
            } else {
                var atomicX = (AtomicConstraintExpression)x!;
                var atomicY = (AtomicConstraintExpression)y!;

                return atomicX.ConstraintType == atomicY.ConstraintType &&
                    atomicX.Right.ToString()!.Equals(atomicY.Right.ToString());
            }
        }

        public int GetHashCode([DisallowNull] ConstraintExpression obj) {
            if (obj is NestedConstraintExpression nestedObj) {
                return nestedObj.GetHashCode() *
                    nestedObj.ConstraintType.GetHashCode() *
                    nestedObj.Left.GetHashCode() *
                    nestedObj.Right.GetHashCode();
            } else {
                var atomicObj = (AtomicConstraintExpression)obj;

                return atomicObj.GetHashCode() *
                    atomicObj.ConstraintType.GetHashCode() *
                    atomicObj.Right.GetHashCode();
            }
        }
    }
}
