using Logic.FactoryInterface;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;

namespace Logic.Mapek
{
    public class MapekExecute : IMapekExecute
    {
        private readonly ILogger<MapekExecute> _logger;
        private readonly IFactory _factory;

        public MapekExecute(IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetRequiredService<ILogger<MapekExecute>>();
            _factory = serviceProvider.GetRequiredService<IFactory>();
        }

        public void Execute(SimulationPath optimalConfiguration, PropertyCache propertyCache, bool useSimulatedTwinningTarget)
        {
            _logger.LogInformation("Starting the Execute phase.");

            if (optimalConfiguration == null)
            {
                return;
            }

            foreach (var simulationTick in optimalConfiguration.Simulations)
            {
                foreach (var actuationAction in simulationTick.ActuationActions)
                {
                    ExecuteActuationAction(actuationAction);
                }

                if (!useSimulatedTwinningTarget)
                {
                    // TODO: add a delay to match the duration of a cycle with the simulated interval.
                }
            }

            LogExpectedPropertyValues(optimalConfiguration);
        }

        private void ExecuteActuationAction(ActuationAction actuationAction)
        {
            _logger.LogInformation("Actuating actuator {actuator} with state {actuatorState}.",
                actuationAction.Actuator.Name,
                actuationAction.NewStateValue.ToString());

            var actuator = _factory.GetActuatorDeviceImplementation(actuationAction.Actuator.Name);

            // This cannot be a blocking call to ensure that multiple Actuators in an interval get actuated for the same duration.
            actuator.Actuate(actuationAction.NewStateValue);
        }

        private void ExecuteReconfigurationAction(ReconfigurationAction reconfigurationAction, PropertyCache propertyCache)
        {
            _logger.LogInformation("Reconfiguring property {configurableProperty} with {effect}.",
                reconfigurationAction.ConfigurableParameter.Name,
                reconfigurationAction.NewParameterValue);

            propertyCache.ConfigurableParameters[reconfigurationAction.ConfigurableParameter.Name].Value = reconfigurationAction.NewParameterValue;
        }

        private void LogExpectedPropertyValues(SimulationPath simulationPath)
        {
            if (!simulationPath.Simulations.Any()) {
                return;
            }

            _logger.LogInformation("Expected Property values:");

            foreach (var propertyKeyValue in simulationPath.Simulations.Last().PropertyCache.Properties)
            {
                _logger.LogInformation("{propertyName}: {propertyValue}", propertyKeyValue.Key, propertyKeyValue.Value.Value.ToString());
            }

            foreach (var configurableParameterKeyValue in simulationPath.Simulations.Last().PropertyCache.ConfigurableParameters)
            {
                _logger.LogInformation("{configurableParameterName}: {configurableParameterValue}",
                    configurableParameterKeyValue.Key,
                    configurableParameterKeyValue.Value.Value.ToString());
            }
        }
    }
}
