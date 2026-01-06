using Logic.Mapek;
using Logic.Models.MapekModels;

namespace TestProject.Mocks.ServiceMocks {
    internal class MapekMonitorMock : IMapekMonitor {
        private readonly Cache _cache;

        public MapekMonitorMock(Cache cache) {
            _cache = cache;
        }

        public Cache Monitor() {
            return _cache;
        }
    }
}
