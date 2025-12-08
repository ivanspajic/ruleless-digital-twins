using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Logic.ValueHandlerInterfaces;

namespace Logic.Mapek.Comparers
{
    internal class SimulationPathComparer : IComparer<SimulationPath>
    {
        public SimulationPathComparer(IEnumerable<(PropertyChange First, IValueHandler Second)> enumerable)
        {
            Enumerable = enumerable;
        }

        public IEnumerable<(PropertyChange First, IValueHandler Second)> Enumerable { get; }

        public int Compare(SimulationPath? x, SimulationPath? y)
        {
            var scores = Enumerable.Select(ep => {
                var p = ep.Item1;
                var valueHandler = ep.Item2;
                var comparingProperty = MapekUtilities.GetPropertyFromPropertyCacheByName(x!.Simulations.Last().PropertyCache!, p.Property.Name);
                var targetProperty = MapekUtilities.GetPropertyFromPropertyCacheByName(y!.Simulations.Last().PropertyCache!, p.Property.Name);
                if (p.OptimizeFor == Effect.ValueIncrease)
                {
                    return valueHandler.IncreaseComp(comparingProperty.Value, targetProperty.Value);
                }
                else
                {
                    return -1 * valueHandler.IncreaseComp(comparingProperty.Value, targetProperty.Value);
                }
            }).Sum(s => s);
            return scores;
        }
    }
}
