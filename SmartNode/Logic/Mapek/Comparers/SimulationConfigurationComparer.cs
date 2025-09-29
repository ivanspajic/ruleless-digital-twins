using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Logic.ValueHandlerInterfaces;

namespace Logic.Mapek.Comparers
{
    internal class SimulationConfigurationComparer : IComparer<SimulationConfiguration>
    {
        public SimulationConfigurationComparer(IEnumerable<(PropertyChange First, IValueHandler Second)> enumerable)
        {
            Enumerable = enumerable;
        }

        public IEnumerable<(PropertyChange First, IValueHandler Second)> Enumerable { get; }

        public int Compare(SimulationConfiguration? x, SimulationConfiguration? y)
        {
            var scores = Enumerable.Select(ep => {
                var p = ep.Item1;
                var valueHandler = ep.Item2;
                var comparingProperty = MapekUtilities.GetPropertyFromPropertyCacheByName(x.ResultingPropertyCache, p.Property.Name);
                var targetProperty = MapekUtilities.GetPropertyFromPropertyCacheByName(y.ResultingPropertyCache, p.Property.Name);
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
