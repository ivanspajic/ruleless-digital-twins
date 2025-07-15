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

        public MapekExecute(IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetRequiredService<ILogger<MapekExecute>>();
            _factory = serviceProvider.GetRequiredService<IFactory>();
        }

        public void Execute(SimulationConfiguration optimalConfiguration, PropertyCache propertyCache)
        {
            _logger.LogInformation("Starting the Execute phase.");

            
        }

        private void ExecuteActuationAction(ActuationAction actuationAction)
        {
            _logger.LogInformation("Actuating actuator {actuator} with state {actuatorState}.",
                actuationAction.ActuatorState.Actuator,
                actuationAction.ActuatorState);

            var actuator = _factory.GetActuatorDeviceImplementation(actuationAction.ActuatorState.Actuator.Name);

            // TODO: expand the logic here when we add more actuation methods.
            actuator.Actuate(actuationAction.ActuatorState.Name);
        }

        private void ExecuteReconfigurationAction(ReconfigurationAction reconfigurationAction, PropertyCache propertyCache)
        {
            _logger.LogInformation("Reconfiguring property {configurableProperty} with {effect}.",
                reconfigurationAction.ConfigurableParameter.Name,
                reconfigurationAction.NewParameterValue);

            var valueHandler = _factory.GetValueHandlerImplementation(reconfigurationAction.ConfigurableParameter.OwlType);

            propertyCache.ConfigurableParameters[reconfigurationAction.ConfigurableParameter.Name].Value = reconfigurationAction.NewParameterValue;
        }
    }
}
