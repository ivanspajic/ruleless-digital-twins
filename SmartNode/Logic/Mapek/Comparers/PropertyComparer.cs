using Logic.Models.OntologicalModels;
using System.Diagnostics.CodeAnalysis;

namespace Logic.Mapek.Comparers {
    internal class PropertyComparer : IEqualityComparer<Property> {
        public bool Equals(Property? x, Property? y) {
            return x!.Name.Equals(y!.Name) &&
                x.Value.Equals(y.Value) &&
                x.OwlType.Equals(y.OwlType);
        }

        public int GetHashCode([DisallowNull] Property obj) {
            return obj.GetHashCode() *
                obj.Name.GetHashCode() *
                obj.Value.GetHashCode() *
                obj.OwlType.GetHashCode();
        }
    }
}
