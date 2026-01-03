using Logic.Models.MapekModels;

namespace Logic.Mapek
{
    public interface IMapekPlan
    {
        public (SimulationTreeNode, IEnumerable<Simulation>, SimulationPath) Plan(Cache cache, int lookAheadCycles);
    }
}
