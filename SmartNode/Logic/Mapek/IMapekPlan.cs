using Logic.Models.MapekModels;
using VDS.RDF;

namespace Logic.Mapek
{
    public interface IMapekPlan
    {
        public SimulationConfiguration Plan(IEnumerable<Models.OntologicalModels.Action> actions,
            PropertyCache propertyCache,
            IGraph instanceModel,
            string fmuDirectory,
            int lookAheadCycles);
    }
}
