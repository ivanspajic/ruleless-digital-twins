using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using VDS.RDF;

namespace Logic.Mapek
{
    public interface IMapekAnalyze
    {
        public Tuple<List<Condition>, List<Models.OntologicalModels.Action>> Analyze(IGraph instanceModel,
            PropertyCache propertyCache,
            int configurableParameterGranularity);
    }
}
