using Logic.Models.MapekModels;

namespace Logic.Mapek
{
    public interface IMapekPlan
    {
        public Task<(SimulationTreeNode, SimulationPath)> Plan(Cache cache, int currentMapekCycle);

        public IEnumerable<SimulationPath> GetOptimalSimulationPath(Cache cache, // Review the need for this being public. It's an implementation detail of MapekPlan.
            IEnumerable<SimulationPath> simulationPaths);
    }
}
