﻿using Logic.FactoryInterface;
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

        public void Execute(SimulationConfiguration optimalConfiguration, PropertyCache propertyCache, bool useSimulatedTwinningTarget)
        {
            _logger.LogInformation("Starting the Execute phase.");

            if (optimalConfiguration == null)
            {
                return;
            }

            foreach (var simulationTick in optimalConfiguration.SimulationTicks)
            {
                foreach (var actuationAction in simulationTick.ActionsToExecute)
                {
                    ExecuteActuationAction(actuationAction, simulationTick.TickDurationSeconds);
                }

                if (!useSimulatedTwinningTarget)
                {
                    Thread.Sleep(simulationTick.TickDurationSeconds);
                }
            }

            foreach (var reconfigurationAction in optimalConfiguration.PostTickActions)
            {
                ExecuteReconfigurationAction(reconfigurationAction, propertyCache);
            }

            LogExpectedPropertyValues(optimalConfiguration);
        }

        private void ExecuteActuationAction(ActuationAction actuationAction, double durationSeconds)
        {
            _logger.LogInformation("Actuating actuator {actuator} with state {actuatorState} and duration of {duration} seconds.",
                actuationAction.Actuator.Name,
                actuationAction.NewStateValue.ToString(),
                durationSeconds);

            var actuator = _factory.GetActuatorDeviceImplementation(actuationAction.Actuator.Name);

            // This cannot be a blocking call to ensure that multiple Actuators in an interval get executed for the same duration.
            actuator.Actuate(actuationAction.NewStateValue);
        }

        private void ExecuteReconfigurationAction(ReconfigurationAction reconfigurationAction, PropertyCache propertyCache)
        {
            _logger.LogInformation("Reconfiguring property {configurableProperty} with {effect}.",
                reconfigurationAction.ConfigurableParameter.Name,
                reconfigurationAction.NewParameterValue);

            propertyCache.ConfigurableParameters[reconfigurationAction.ConfigurableParameter.Name].Value = reconfigurationAction.NewParameterValue;
        }

        private void LogExpectedPropertyValues(SimulationConfiguration simulationConfiguration)
        {
            _logger.LogInformation("Expected Property values:");

            foreach (var propertyKeyValue in simulationConfiguration.ResultingPropertyCache.Properties)
            {
                _logger.LogInformation("{propertyName}: {propertyValue}", propertyKeyValue.Key, propertyKeyValue.Value.Value.ToString());
            }

            foreach (var configurableParameterKeyValue in simulationConfiguration.ResultingPropertyCache.ConfigurableParameters)
            {
                _logger.LogInformation("{configurableParameterName}: {configurableParameterValue}",
                    configurableParameterKeyValue.Key,
                    configurableParameterKeyValue.Value.Value.ToString());
            }
        }
    }
}
