using System.Diagnostics.CodeAnalysis;

namespace Logic.Mapek.Comparers
{
    internal class ActionSetEqualityComparer : IEqualityComparer<HashSet<Models.OntologicalModels.Action>>
    {
        public bool Equals(HashSet<Models.OntologicalModels.Action>? x, HashSet<Models.OntologicalModels.Action>? y)
        {
            return x!.SetEquals(y!);
        }

        public int GetHashCode([DisallowNull] HashSet<Models.OntologicalModels.Action> obj)
        {
            var hashCode = 1;

            foreach (var element in obj)
            {
                hashCode *= element.Name.GetHashCode();
            }

            return hashCode;
        }
    }
}
