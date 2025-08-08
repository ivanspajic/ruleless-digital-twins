using System.Diagnostics.CodeAnalysis;

namespace Logic.Mapek.EqualityComparers
{
    internal class SetEqualityComparer<T> : IEqualityComparer<HashSet<T>>
    {
        public bool Equals(HashSet<T>? x, HashSet<T>? y)
        {
            return x!.SetEquals(y!);
        }

        public int GetHashCode([DisallowNull] HashSet<T> obj)
        {
            var hashCode = 1;

            foreach (var element in obj)
            {
                hashCode *= element!.GetHashCode();
            }

            return hashCode;
        }
    }
}
