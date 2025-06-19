using Models;
using Models.MapekModels;
using VDS.RDF;

namespace Logic.Mapek
{
    public interface IMapekAnalyze
    {
        public Tuple<List<OptimalCondition>, List<Models.Action>> Analyze(IGraph instanceModel,
            PropertyCache propertyCache);
    }
}
