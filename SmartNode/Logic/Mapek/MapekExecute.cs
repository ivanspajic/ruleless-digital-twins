using Logic.FactoryInterface;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;

namespace Logic.Mapek
{
    internal class MapekExecute : IMapekExecute
    {
        private readonly ILogger<MapekExecute> _logger;
        private readonly IFactory _factory;

        private const int MaximumParallelThreads = 4;

        public MapekExecute(IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetRequiredService<ILogger<MapekExecute>>();
            _factory = serviceProvider.GetRequiredService<IFactory>();
        }

        public void Execute(SimulationConfiguration optimalConfiguration, PropertyCache propertyCache)
        {
            _logger.LogInformation("Starting the Execute phase.");

            foreach (var simulationTick in optimalConfiguration.SimulationTicks)
            {
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = MaximumParallelThreads
                };

                Parallel.ForEach(simulationTick.ActionsToExecute, parallelOptions, (actuationAction) =>
                {
                    ExecuteActuationAction(actuationAction, simulationTick.TickDurationSeconds);
                });
            }

            foreach (var reconfigurationAction in optimalConfiguration.PostTickActions)
            {
                ExecuteReconfigurationAction(reconfigurationAction, propertyCache);
            }
        }

        private void ExecuteActuationAction(ActuationAction actuationAction, double durationSeconds)
        {
            _logger.LogInformation("Actuating actuator {actuator} with state {actuatorState} and duration of {duration} seconds.",
                actuationAction.ActuatorState.Actuator,
                actuationAction.ActuatorState,
                durationSeconds);

            var actuator = _factory.GetActuatorDeviceImplementation(actuationAction.ActuatorState.Actuator.Name);

            actuator.Actuate(actuationAction.ActuatorState.Name, durationSeconds);
        }

        private void ExecuteReconfigurationAction(ReconfigurationAction reconfigurationAction, PropertyCache propertyCache)
        {
            _logger.LogInformation("Reconfiguring property {configurableProperty} with {effect}.",
                reconfigurationAction.ConfigurableParameter.Name,
                reconfigurationAction.NewParameterValue);

            propertyCache.ConfigurableParameters[reconfigurationAction.ConfigurableParameter.Name].Value = reconfigurationAction.NewParameterValue;
        }
    }
}
