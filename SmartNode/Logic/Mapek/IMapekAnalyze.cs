using Models;
using Models.MapekModels;
using VDS.RDF;

namespace Logic.Mapek
{
    public interface IMapekAnalyze
    {
        public Tuple<IEnumerable<OptimalCondition>, IEnumerable<Models.Action>> Analyze(IGraph instanceModel, PropertyCache propertyCache);
    }
}
