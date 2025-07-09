using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using VDS.RDF;

namespace Logic.Mapek
{
    internal interface IMapekAnalyze
    {
        public Tuple<IEnumerable<OptimalCondition>, IEnumerable<Models.OntologicalModels.Action>> Analyze(IGraph instanceModel, PropertyCache propertyCache);
    }
}
