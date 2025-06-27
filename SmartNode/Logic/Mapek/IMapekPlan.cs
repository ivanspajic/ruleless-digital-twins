using Models;
using Models.MapekModels;

namespace Logic.Mapek
{
    public interface IMapekPlan
    {
        public IEnumerable<Models.Action> Plan(IEnumerable<OptimalCondition> optimalConditions,
            IEnumerable<Models.Action> actions,
            PropertyCache propertyCache);
    }
}
