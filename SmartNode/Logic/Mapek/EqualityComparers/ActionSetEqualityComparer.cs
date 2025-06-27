using System.Diagnostics.CodeAnalysis;

namespace Logic.Mapek.EqualityComparers
{
    public class ActionSetEqualityComparer : IEqualityComparer<HashSet<Models.Action>>
    {
        public bool Equals(HashSet<Models.Action>? x, HashSet<Models.Action>? y)
        {
            return x!.SetEquals(y!);
        }

        public int GetHashCode([DisallowNull] HashSet<Models.Action> obj)
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
