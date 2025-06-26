using Models;
using Models.MapekModels;
using VDS.RDF;

namespace Logic.Mapek
{
    public interface IMapekAnalyze
    {
        public Tuple<OptimalCondition[], Models.Action[]> Analyze(IGraph instanceModel,
            PropertyCache propertyCache);
    }
}
