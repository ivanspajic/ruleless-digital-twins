using Logic.Models.OntologicalModels;
using System.Diagnostics.CodeAnalysis;

namespace Logic.Mapek.EqualityComparers
{
    internal class ActuationActionSetEqualityComparer : IEqualityComparer<HashSet<ActuationAction>>
    {
        public bool Equals(HashSet<ActuationAction>? x, HashSet<ActuationAction>? y)
        {
            return x!.SetEquals(y!);
        }

        public int GetHashCode([DisallowNull] HashSet<ActuationAction> obj)
        {
            var hashCode = 0;

            foreach (var element in obj)
            {
                hashCode *= element.Name.GetHashCode();
            }

            return hashCode;
        }
    }
}
