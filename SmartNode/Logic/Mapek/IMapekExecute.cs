using Logic.Models.MapekModels;

namespace Logic.Mapek
{
    public interface IMapekExecute
    {
        public void Execute(SimulationPath simulationPath, Cache propertyCache, bool useSimulatedTwinningTarget);
    }
}
