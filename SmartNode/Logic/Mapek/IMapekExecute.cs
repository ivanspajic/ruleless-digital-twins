using Logic.Models.MapekModels;

namespace Logic.Mapek
{
    public interface IMapekExecute
    {
        public void Execute(SimulationPath simulationPath, PropertyCache propertyCache, bool useSimulatedTwinningTarget);
    }
}
