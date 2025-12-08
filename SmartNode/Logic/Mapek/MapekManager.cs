using Logic.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TestProject")]

namespace Logic.Mapek {
    public class MapekManager : IMapekManager {
        private const int SleepyTimeMilliseconds = 2_000;
        // Decides on the number of MAPE-K cycles to look ahead for and thus the number of simulation steps to run. For example,
        // setting this value to 1 means only simulating for the current cycle, while setting it to 4 means simulating for the next
        // 4 cycles.
        private const int LookAheadCycles = 4;
        // Decides on the granularity of steps in increasing/decreasing ConfigurableParameter values.
        private const int ConfigurableParameterGranularity = 7;

        private readonly ILogger<IMapekManager> _logger;
        private readonly IMapekMonitor _mapekMonitor;
        private readonly IMapekAnalyze _mapekAnalyze;
        private readonly IMapekPlan _mapekPlan;
        private readonly IMapekExecute _mapekExecute;
        private readonly IMapekKnowledge _mapekKnowledge;

        private bool _isLoopActive = false;

        public MapekManager(IServiceProvider serviceProvider) {
            _logger = serviceProvider.GetRequiredService<ILogger<IMapekManager>>();
            _mapekMonitor = serviceProvider.GetRequiredService<IMapekMonitor>();
            _mapekAnalyze = serviceProvider.GetRequiredService<IMapekAnalyze>();
            _mapekPlan = serviceProvider.GetRequiredService<IMapekPlan>();
            _mapekExecute = serviceProvider.GetRequiredService<IMapekExecute>();
            _mapekKnowledge = serviceProvider.GetRequiredService<IMapekKnowledge>();
        }

        public void StartLoop(string instanceModelFilePath, string fmuDirectory, string dataDirectory, int maxRound = -1, bool simulateTwinningTarget = false) {
            _isLoopActive = true;
            RunMapekLoop(instanceModelFilePath, fmuDirectory, dataDirectory, maxRound, simulateTwinningTarget);
        }

        public void StopLoop() {
            _isLoopActive = false;
        }

        private void RunMapekLoop(string instanceModelFilePath, string fmuDirectory, string dataDirectory, int maxRound = -1, bool simulateTwinningTarget = false) {
            _logger.LogInformation("Starting the MAPE-K loop. (maxRounds= {maxRound})", maxRound);

            var currentRound = 0;

            while (_isLoopActive) {
                if (maxRound > -1) {
                    _logger.LogInformation("MAPE-K rounds left: {maxRound})", maxRound);
                }

                // Reload the instance model for each cycle to ensure dynamic model updates are captured.
                _mapekKnowledge.LoadModelsFromKnowledgeFromKnowledgeBase(); // This makes sense in theory but won't work without the Factory updating as well.

                // Monitor - Observe all hard and soft Sensor values.
                var propertyCache = _mapekMonitor.Monitor();

                // Analyze - Out of all possible Actions, filter out the irrelevant ones based on current Property values and return
                // them with all OptimalConditions.
                // var optimalConditionsAndActions = _mapekAnalyze.Analyze(instanceModel, propertyCache, ConfigurableParameterGranularity);

                // Plan - Simulate all Actions and check that they mitigate OptimalConditions and optimize the system to get the most optimal configuration.
                var optimalSimulationPath = _mapekPlan.Plan(propertyCache, LookAheadCycles);
                // Execute - Execute the Actuators with the appropriate ActuatorStates and/or adjust the values of ReconfigurableParameters.
                _mapekExecute.Execute(optimalSimulationPath, propertyCache, simulateTwinningTarget);

                // Write MAPE-K state to CSV.
                CsvUtils.WritePropertyStatesToCsv(dataDirectory, currentRound, propertyCache);
                CsvUtils.WriteActuatorStatesToCsv(dataDirectory, currentRound, optimalSimulationPath);

                if (maxRound > 0) {
                    maxRound--;
                }
                if (maxRound == 0) {
                    _isLoopActive = false;
                    break; // We can sleep when we're dead.
                }

                currentRound++;

                // Thread.Sleep(SleepyTimeMilliseconds);
            }
        }
    }
}