using Logic.FactoryInterface;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;

namespace Logic.Mapek
{
    public class MapekExecute : IMapekExecute
    {
        private readonly ILogger<IMapekExecute> _logger;
        private readonly IFactory _factory;

        public MapekExecute(IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetRequiredService<ILogger<IMapekExecute>>();
            _factory = serviceProvider.GetRequiredService<IFactory>();
        }

        public async Task Execute(Simulation simulation)
        {
            _logger.LogInformation("Starting the Execute phase.");

            foreach (var action in simulation.Actions)
            {
                if (action is ActuationAction actuationAction) {
                    ExecuteActuationAction(actuationAction);
                } else {
                    ExecuteReconfigurationAction((ReconfigurationAction)action);
                }
            }

            LogExpectedPropertyValues(simulation);
        }

        private void ExecuteActuationAction(ActuationAction actuationAction)
        {
            _logger.LogInformation("Actuating actuator {actuator} with state {actuatorState}.",
                actuationAction.Actuator.Name,
                actuationAction.NewStateValue.ToString());

            var actuator = _factory.GetActuatorImplementation(actuationAction.Actuator.Name);

            // This cannot be a blocking call to ensure that multiple Actuators in an interval get actuated for the same duration.
            actuator.Actuate(actuationAction.NewStateValue);
        }

        private void ExecuteReconfigurationAction(ReconfigurationAction reconfigurationAction)
        {
            _logger.LogInformation("Reconfiguring property {configurableProperty} with {effect}.",  
                reconfigurationAction.ConfigurableParameter.Name,
                reconfigurationAction.NewParameterValue);

            var configurableParameterImplementation = _factory.GetConfigurableParameterImplementation(reconfigurationAction.ConfigurableParameter.Name);
            configurableParameterImplementation.UpdateConfigurableParameter(reconfigurationAction.ConfigurableParameter.Name, reconfigurationAction.NewParameterValue);
        }

        private void LogExpectedPropertyValues(Simulation simulation)
        {
            _logger.LogInformation("Expected Property values:");

            foreach (var propertyKeyValue in simulation.PropertyCache!.Properties)
            {
                _logger.LogInformation("{propertyName}: {propertyValue}", propertyKeyValue.Key, propertyKeyValue.Value.Value.ToString());
            }

            foreach (var configurableParameterKeyValue in simulation.PropertyCache.ConfigurableParameters)
            {
                _logger.LogInformation("{configurableParameterName}: {configurableParameterValue}",
                    configurableParameterKeyValue.Key,
                    configurableParameterKeyValue.Value.Value.ToString());
            }
        }
    }
}
