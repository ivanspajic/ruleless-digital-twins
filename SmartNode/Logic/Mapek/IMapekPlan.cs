using Models;
using Models.MapekModels;

namespace Logic.Mapek
{
    public interface IMapekPlan
    {
        public Plan Plan(Tuple<List<Mitigation>, List<Models.Action>> mitigationAndOptimizationTuple);
    }
}
