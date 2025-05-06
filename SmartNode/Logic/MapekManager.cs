using Microsoft.Extensions.Logging;

namespace Logic
{
    public class MapekManager : IMapekManager
    {
        private readonly ILogger<MapekManager> _logger;

        private bool _isLoopActive = false;

        public MapekManager(ILogger<MapekManager> logger)
        {
            _logger = logger;

            _logger.LogInformation("test log, lelelelelele");
        }

        public void StartLoop()
        {
            _isLoopActive = true;

            RunMapekLoop();
        }

        public void StopLoop()
        {
            _isLoopActive = false;
        }

        private void RunMapekLoop()
        {
            InitializeGraph();

            while (_isLoopActive)
            {
                // Monitor
                // Analyze
                // Plan
                // Execute
            }
        }

        private void InitializeGraph()
        {

        }

        private void Monitor()
        {

        }
    }
}
