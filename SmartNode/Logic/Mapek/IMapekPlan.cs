using Logic.Models.MapekModels;

namespace Logic.Mapek
{
    public interface IMapekPlan
    {
        public Task<(SimulationTreeNode, SimulationPath)> Plan(Cache cache);
    }
}
