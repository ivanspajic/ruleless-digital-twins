using Logic.Models.MapekModels;

namespace Logic.Mapek
{
    public interface IStreamingSimulationProvider
    {
        void Queue(Simulation parent, Simulation simulation);
        void Starting(Simulation simulation);
        void Stopped(Simulation simulation);
        // Called at the beginning of each MAPE-K cycle, before any simulations new are started.
        // The consumer can decide if it wants to reset or collapse the tree: all following
        // simulations will be (somewhere) below this node only, until the next Reset().
        // May need a review if we go over to a workpool.
        void Reset(Simulation parent);
    }

    public class NullStreamingSimulationProvider : IStreamingSimulationProvider
    {
        public void Queue(Simulation parent, Simulation simulation) { }
        public void Reset(Simulation nodeItem) { }
        public void Starting(Simulation simulation) { }
        public void Stopped(Simulation simulation) { }
    }
}