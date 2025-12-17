using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;

namespace Logic.Mapek
{
    public interface IMapekExecute
    {
        public void Execute(SimulationPath simulationPath, IDictionary<string, ConfigurableParameter> configurableParameters, bool useSimulatedTwinningTarget);
    }
}
