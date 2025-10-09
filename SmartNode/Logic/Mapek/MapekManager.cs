using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;
using VDS.RDF;
using VDS.RDF.Parsing;

[assembly: InternalsVisibleTo("TestProject")]

namespace Logic.Mapek
{
    public class MapekManager : IMapekManager
    {
        private const int SleepyTimeMilliseconds = 2_000;
        // Decides on the number of intervals in ActuationAction simulations.
        private const int ActuationSimulationGranularity = 4;
        // Decides on the granularity of steps in increasing/decreasing ConfigurableParameter values.
        private const int ConfigurableParameterGranularity = 7;

        private readonly ILogger<MapekManager> _logger;
        private readonly IMapekMonitor _mapekMonitor;
        private readonly IMapekAnalyze _mapekAnalyze;
        private readonly IMapekPlan _mapekPlan;
        private readonly IMapekExecute _mapekExecute;

        private bool _isLoopActive = false;

        public MapekManager(IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetRequiredService<ILogger<MapekManager>>();
            _mapekMonitor = new MapekMonitor(serviceProvider);
            _mapekAnalyze = new MapekAnalyze(serviceProvider);
            _mapekPlan = new MapekPlan(serviceProvider);
            _mapekExecute = new MapekExecute(serviceProvider);
        }

        public void StartLoop(string instanceModelFilePath, string fmuDirectory, int maxRound = -1)
        {
            _isLoopActive = true;
            RunMapekLoop(instanceModelFilePath, fmuDirectory, maxRound);
        }

        public void StopLoop()
        {
            _isLoopActive = false;
        }

        private void RunMapekLoop(string instanceModelFilePath, string fmuDirectory, int maxRound = -1)
        {
            _logger.LogInformation("Starting the MAPE-K loop. (maxRounds= {maxRound})", maxRound);

            while (_isLoopActive)
            {
                if (maxRound > -1) {
                    _logger.LogInformation("MAPE-K rounds left: {maxRound})", maxRound);
                }
                // Load the instance model into a graph object. Doing this inside the loop allows for dynamic model updates at
                // runtime.
                var instanceModel = Initialize(instanceModelFilePath);

                // If nothing was loaded, don't start the loop.
                if (instanceModel.IsEmpty)
                {
                    throw new Exception("There is nothing in the instance model graph.");
                }

                // Monitor - Observe all hard and soft Sensor values.
                var propertyCache = _mapekMonitor.Monitor(instanceModel);
                // Analyze - Out of all possible Actions, filter out the irrelevant ones based on current Property values and return
                // them with all OptimalConditions.
                var optimalConditionsAndActions = _mapekAnalyze.Analyze(instanceModel, propertyCache, ConfigurableParameterGranularity);
                // Plan - Simulate all Actions and check that they mitigate OptimalConditions and optimize the system to get the most optimal configuration.
                var optimalConfiguration = _mapekPlan.Plan(optimalConditionsAndActions.Item1, optimalConditionsAndActions.Item2, propertyCache, instanceModel, fmuDirectory, ActuationSimulationGranularity);
                // Execute - Execute the Actuators with the appropriate ActuatorStates and/or adjust the values of ReconfigurableParameters.
                _mapekExecute.Execute(optimalConfiguration, propertyCache);

                if (maxRound > 0)
                {
                    maxRound--;
                }
                if (maxRound == 0)
                {
                    _isLoopActive = false;
                    break; // We can sleep when we're dead.
                }

                // Thread.Sleep(SleepyTimeMilliseconds);
            }
        }

        private Graph Initialize(string instanceModelFilePath)
        {
            _logger.LogInformation("Loading instance model file contents from {filePath}.", instanceModelFilePath);

            var instanceModel = new Graph();

            var turtleParser = new TurtleParser();
            turtleParser.Load(instanceModel, instanceModelFilePath);

            return instanceModel;
        }
    }
}
