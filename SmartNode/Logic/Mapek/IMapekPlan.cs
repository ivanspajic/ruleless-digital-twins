using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using VDS.RDF;

namespace Logic.Mapek
{
    public interface IMapekPlan
    {
        public SimulationConfiguration Plan(IEnumerable<OptimalCondition> optimalConditions,
            IEnumerable<Models.OntologicalModels.Action> actions,
            PropertyCache propertyCache,
            IGraph instanceModel,
            string fmuDirectory,
            int actuationSimulationGranularity);
    }
}
