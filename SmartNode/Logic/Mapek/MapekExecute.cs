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

        public void Execute(SimulationPath optimalConfiguration, IDictionary<string, ConfigurableParameter> configurableParameters, bool useSimulatedTwinningTarget)
        {
            _logger.LogInformation("Starting the Execute phase.");

            if (optimalConfiguration == null)
            {
                return;
            }

            foreach (var simulationTick in optimalConfiguration.Simulations)
            {
                foreach (var action in simulationTick.Actions)
                {
                    if (action is ActuationAction actuationAction) {
                        ExecuteActuationAction(actuationAction);
                    } else {
                        ExecuteReconfigurationAction((ReconfigurationAction)action);
                    }
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

        private void ExecuteReconfigurationAction(ReconfigurationAction reconfigurationAction)
        {
            _logger.LogInformation("Reconfiguring property {configurableProperty} with {effect}.",
                reconfigurationAction.ConfigurableParameter.Name,
                reconfigurationAction.NewParameterValue);

            // TODO: figure out what this looks like. The best thing to do is probably to keep the symmetry between Actuators and ConfigurableParameters.
            // This will, however, probably need some form of "submit" action to ensure all Properties get sent to the cyber system simultaneously.
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
