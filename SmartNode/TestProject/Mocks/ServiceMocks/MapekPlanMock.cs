using Logic.Mapek;
using Logic.Models.MapekModels;

namespace TestProject.Mocks.ServiceMocks {
    internal class MapekPlanMock : IMapekPlan {
        private readonly (SimulationTreeNode, SimulationPath) _simulationTuple;

        public MapekPlanMock((SimulationTreeNode, SimulationPath) simulationTuple) {
            _simulationTuple = simulationTuple;
        }

        public IEnumerable<SimulationPath> GetOptimalSimulationPath(Cache cache, IEnumerable<SimulationPath> simulationPaths)
        {
            throw new NotImplementedException(); // XXX Unclear who needs this?
        }

        public async Task<(SimulationTreeNode, SimulationPath)> Plan(Cache cache, int currentMapekCycle) {
            return _simulationTuple;
        }
    }
}
