using Logic.Models.MapekModels;

namespace Logic.Mapek
{
    public interface IMapekExecute
    {
        public void Execute(Simulation simulation, bool useSimulatedTwinningTarget);
    }
}
