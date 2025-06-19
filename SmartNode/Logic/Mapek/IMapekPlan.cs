using Models;

namespace Logic.Mapek
{
    public interface IMapekPlan
    {
        public List<Models.Action> Plan(Tuple<List<OptimalCondition>, List<Models.Action>> optimalConditionsAndActions);
    }
}
