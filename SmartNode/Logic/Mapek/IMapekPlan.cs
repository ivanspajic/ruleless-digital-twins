using Models.MapekModels;
using Models.OntologicalModels;

namespace Logic.Mapek
{
    public interface IMapekPlan
    {
        public IEnumerable<Models.OntologicalModels.Action> Plan(IEnumerable<OptimalCondition> optimalConditions,
            IEnumerable<Models.OntologicalModels.Action> actions,
            PropertyCache propertyCache);
    }
}
