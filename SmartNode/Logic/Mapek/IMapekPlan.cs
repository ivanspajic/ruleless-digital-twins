using Logic.Models.MapekModels;

namespace Logic.Mapek
{
    public interface IMapekPlan
    {
        public Task<(SimulationTreeNode, SimulationPath)> Plan(Cache cache);
        public IEnumerable<SimulationPath> GetOptimalSimulationPath(Cache cache,
            IEnumerable<SimulationPath> simulationPaths);
    }
}
