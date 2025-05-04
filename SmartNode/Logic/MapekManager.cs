namespace Logic
{
    public class MapekManager : IMapekManager
    {
        private bool _isLoopActive = false;

        public MapekManager()
        {

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
            InitializePlatform();

            while (_isLoopActive)
            {
                // Monitor - should return properties and executions
                // Analyze - use the properties and executions to narrow down and return the relevant executions
                // Plan - plan for a strategy with specific executions from the optimization information from the instance model
                // Execute - send the strategy for execution to the respective actuators or reconfiguration property updaters
            }
        }

        private void InitializePlatform()
        {

        }

        private void Monitor()
        {

        }
    }
}
