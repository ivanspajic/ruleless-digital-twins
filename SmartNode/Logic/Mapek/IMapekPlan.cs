using Logic.Models.MapekModels;

namespace Logic.Mapek
{
    public interface IMapekPlan
    {
        public SimulationConfiguration Plan(PropertyCache propertyCache, string fmuDirectory, int lookAheadCycles);
    }
}
