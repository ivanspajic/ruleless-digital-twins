using Logic.Models.MapekModels;

namespace Logic.Mapek
{
    public interface IMapekPlan
    {
        public SimulationPath Plan(Cache propertyCache, int lookAheadCycles);
    }
}
