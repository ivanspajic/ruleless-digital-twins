using Models;
using Models.MapekModels;
using VDS.RDF;

namespace Logic.Mapek
{
    public interface IMapekAnalyze
    {
        public Tuple<List<OptimalCondition>, List<ExecutionPlan>> Analyze(IGraph instanceModel, PropertyCache propertyCache);
    }
}
