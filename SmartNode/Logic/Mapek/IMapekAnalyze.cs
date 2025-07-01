using Models.MapekModels;
using Models.OntologicalModels;
using VDS.RDF;

namespace Logic.Mapek
{
    public interface IMapekAnalyze
    {
        public Tuple<IEnumerable<OptimalCondition>, IEnumerable<Models.OntologicalModels.Action>> Analyze(IGraph instanceModel, PropertyCache propertyCache);
    }
}
