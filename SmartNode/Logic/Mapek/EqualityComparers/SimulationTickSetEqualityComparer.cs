using Logic.Models.MapekModels;
using System.Diagnostics.CodeAnalysis;

namespace Logic.Mapek.EqualityComparers
{
    internal class SimulationTickSetEqualityComparer : IEqualityComparer<HashSet<SimulationTick>>
    {
        public bool Equals(HashSet<SimulationTick>? x, HashSet<SimulationTick>? y)
        {
            return x!.SetEquals(y!);
        }

        public int GetHashCode([DisallowNull] HashSet<SimulationTick> obj)
        {
            var hashCode = 0;

            foreach (var element in obj)
            {
                hashCode *= element.TickIndex.GetHashCode();
            }

            return hashCode;
        }
    }
}
