using Logic.Mapek;
using Logic.Models.MapekModels;

namespace TestProject.Mocks.ServiceMocks {
    internal class MapekPlanMock : IMapekPlan {
        private readonly (SimulationTreeNode, SimulationPath) _simulationTuple;

        public MapekPlanMock((SimulationTreeNode, SimulationPath) simulationTuple) {
            _simulationTuple = simulationTuple;
        }

        public async Task<(SimulationTreeNode, SimulationPath)> Plan(Cache cache) {
            return _simulationTuple;
        }
    }
}
