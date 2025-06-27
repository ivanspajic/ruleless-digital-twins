using Logic.FactoryInterface;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Models;
using Models.MapekModels;

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

        public void Execute(IEnumerable<Models.Action> actions, PropertyCache propertyCache)
        {
            _logger.LogInformation("Starting the Execute phase.");

            foreach (var action in actions)
            {
                if (action is ActuationAction actuationAction)
                {
                    ExecuteActuationAction(actuationAction);
                }
                else
                {
                    ExecuteReconfigurationAction((ReconfigurationAction)action, propertyCache);
                }
            }
        }

        private void ExecuteActuationAction(ActuationAction actuationAction)
        {
            _logger.LogInformation("Actuating actuator {actuator} with state {actuatorState}.",
                actuationAction.ActuatorState.Actuator,
                actuationAction.ActuatorState);

            var actuator = _factory.GetActuatorImplementation(actuationAction.ActuatorState.Actuator);

            // TODO: expand the logic here when we add more actuation methods.
            actuator.Actuate(actuationAction.ActuatorState.Name);
        }

        private void ExecuteReconfigurationAction(ReconfigurationAction reconfigurationAction, PropertyCache propertyCache)
        {
            _logger.LogInformation("Reconfiguring property {configurableProperty} with {effect}.",
                reconfigurationAction.ConfigurableParameter.Name,
                reconfigurationAction.Effect);

            var valueHandler = _factory.GetSensorValueHandlerImplementation(reconfigurationAction.ConfigurableParameter.OwlType);

            propertyCache.ConfigurableParameters[reconfigurationAction.ConfigurableParameter.Name].Value =
                valueHandler.ChangeValueByAmount(reconfigurationAction.ConfigurableParameter.Value,
                    reconfigurationAction.AltersBy,
                    reconfigurationAction.Effect);
        }
    }
}
