using Logic.Models.MapekModels;

namespace Logic.Mapek
{
    public interface IMapekPlan
    {
        public (SimulationTreeNode, SimulationPath) Plan(Cache cache, int lookAheadCycles);
    }
}
