using Logic.Models.MapekModels;
using VDS.RDF;

namespace Logic.Mapek
{
    public interface IMapekAnalyze
    {
        public List<Models.OntologicalModels.Action> Analyze(IGraph instanceModel, PropertyCache propertyCache);
    }
}
