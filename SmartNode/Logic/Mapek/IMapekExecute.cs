using Logic.Models.MapekModels;

namespace Logic.Mapek
{
    public interface IMapekExecute
    {
        public void Execute(SimulationConfiguration simulationConfiguration, PropertyCache propertyCache, bool useSimulatedTwinningTarget);
    }
}
