using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using VDS.RDF;

namespace Logic.Mapek
{
    public interface IMapekAnalyze
    {
        public Tuple<List<OptimalCondition>, List<Models.OntologicalModels.Action>> Analyze(IGraph instanceModel,
            PropertyCache propertyCache,
            int configurableParameterGranularity);
    }
}
