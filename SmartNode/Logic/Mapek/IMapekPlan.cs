using Models.MapekModels;

namespace Logic.Mapek
{
    public interface IMapekPlan
    {
        public List<Models.Action> Plan(Tuple<List<Mitigation>, List<Models.Action>> mitigationAndOptimizationTuple);
    }
}
