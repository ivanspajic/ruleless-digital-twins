using Logic.Models.MapekModels;

namespace Logic.Mapek
{
    internal interface IMapekExecute
    {
        public void Execute(SimulationConfiguration simulationConfiguration, PropertyCache propertyCache, bool useSimulatedTwinningTarget);
    }
}
