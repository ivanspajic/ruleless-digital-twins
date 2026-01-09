using Logic.Mapek;
using Logic.Models.MapekModels;

namespace TestProject.Mocks.ServiceMocks {
    internal class MapekPlanMock : IMapekPlan {
        private readonly (SimulationTreeNode, SimulationPath) _simulationTuple;

        public MapekPlanMock((SimulationTreeNode, SimulationPath) simulationTuple) {
            _simulationTuple = simulationTuple;
        }

        public (SimulationTreeNode, SimulationPath) Plan(Cache cache, int lookAheadCycles) {
            return _simulationTuple;
        }
    }
}
