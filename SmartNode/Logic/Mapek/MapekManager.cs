using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VDS.RDF;
using VDS.RDF.Parsing;

namespace Logic.Mapek
{
    public class MapekManager : IMapekManager
    {
        private const int SleepyTimeMilliseconds = 5_000;

        private readonly ILogger<MapekManager> _logger;
        private readonly IMapekMonitor _mapekMonitor;
        private readonly IMapekAnalyze _mapekAnalyze;
        private readonly IMapekPlan _mapekPlan;
        private readonly IMapekExecute _mapekExecute;

        private bool _isLoopActive = false;

        public MapekManager(IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetRequiredService<ILogger<MapekManager>>();
            _mapekMonitor = serviceProvider.GetRequiredService<IMapekMonitor>();
            _mapekAnalyze = serviceProvider.GetRequiredService<IMapekAnalyze>();
            _mapekPlan = serviceProvider.GetRequiredService<IMapekPlan>();
            _mapekExecute = serviceProvider.GetRequiredService<IMapekExecute>();
        }

        public void StartLoop(string instanceModelFilePath)
        {
            _isLoopActive = true;

            RunMapekLoop(instanceModelFilePath);
        }

        public void StopLoop()
        {
            _isLoopActive = false;
        }

        private void RunMapekLoop(string instanceModelFilePath)
        {
            _logger.LogInformation("Starting the MAPE-K loop.");

            while (_isLoopActive)
            {
                // Load the instance model into a graph object. Doing this inside the loop allows for dynamic model updates at
                // runtime.
                var instanceModel = Initialize(instanceModelFilePath);

                // If nothing was loaded, don't start the loop.
                if (instanceModel.IsEmpty)
                {
                    _logger.LogError("There is nothing in the graph. Terminated MAPE-K loop.");

                    throw new Exception("The graph is empty.");
                }

                // Monitor - Observe all hard and soft Sensor values.
                var propertyCache = _mapekMonitor.Monitor(instanceModel);
                // Analyze - Out of all possible ExecutionPlans, filter out the irrelevant ones based on current Property values.
                var optimalConditionAndExecutionPlanTuple = _mapekAnalyze.Analyze(instanceModel, propertyCache);
                // Plan
                //var executionPlans = _mapekPlan.Plan(optimalConditionAndExecutionPlanTuple.Item1, optimalConditionAndExecutionPlanTuple.Item2);
                // Execute
                //var somethingToReturn = _mapekPlan.Execute(executionPlans);

                Thread.Sleep(SleepyTimeMilliseconds);
            }
        }

        private Graph Initialize(string instanceModelFilePath)
        {
            _logger.LogInformation("Loading instance model file contents from {filePath}.", instanceModelFilePath);

            var instanceModel = new Graph();

            var turtleParser = new TurtleParser();
            try
            {
                turtleParser.Load(instanceModel, instanceModelFilePath);
            }
            catch (Exception exception)
            {    
                _logger.LogError(exception, "Exception while loading file contents: {exceptionMessage}", exception.Message);

                throw;
            }

            return instanceModel;
        }
    }
}
