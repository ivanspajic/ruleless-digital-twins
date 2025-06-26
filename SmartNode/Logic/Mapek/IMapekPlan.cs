using Models;
using Models.MapekModels;

namespace Logic.Mapek
{
    public interface IMapekPlan
    {
        public Models.Action[] Plan(OptimalCondition[] optimalConditions,
            Models.Action[] actions,
            PropertyCache propertyCache);
    }
}
