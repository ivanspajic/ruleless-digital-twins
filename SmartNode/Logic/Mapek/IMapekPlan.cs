using Logic.Models.MapekModels;

namespace Logic.Mapek
{
    public interface IMapekPlan
    {
        public SimulationPath Plan(PropertyCache propertyCache, int lookAheadCycles);
    }
}
