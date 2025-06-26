using Models;
using Models.MapekModels;

namespace Logic.Mapek
{
    public interface IMapekPlan
    {
        public List<Models.Action> Plan(List<OptimalCondition> optimalConditions,
            List<Models.Action> actions,
            PropertyCache propertyCache);
    }
}
