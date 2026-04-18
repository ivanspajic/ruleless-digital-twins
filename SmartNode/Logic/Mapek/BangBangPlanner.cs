using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Logic.Mapek {
    public class BangBangPlanner : IBangBangPlanner {
        private readonly ILogger<IBangBangPlanner> _logger;

        // Hard-coded OptimalConditions bounds help avoid unnecessary ontological expression complexity. These reflect the same OptimalConditions present in the M370
        // instance model and are enough for this proof of concept.
        private const double MaximumOfficeTemperature = 22.0;
        private const double MinimumOfficeTemperature = 18.0;
        private const double MaximumOfficeHumidity = 50.0;
        private const double MinimumOfficeHumidity = 30.0;

        private const string HeaterName = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater";
        private const string FloorHeatingName = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating";
        private const string DehumidifierName = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier";
        private const string RoomTemperatureName = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#RoomTemperature";
        private const string RoomHumidityName = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#RoomHumidity";

        public BangBangPlanner(IServiceProvider serviceProvider) {
            _logger = serviceProvider.GetRequiredService<ILogger<IBangBangPlanner>>();
        }

        public Simulation Plan(Cache cache) {
            _logger.LogInformation("Firing the bang-bang controller.");

            var heater = cache.Actuators[HeaterName];
            var floorHeating = cache.Actuators[FloorHeatingName];
            var dehumidifier = cache.Actuators[DehumidifierName];

            var officeTemperature = (double)cache.PropertyCache.Properties[RoomTemperatureName].Value;
            var officeHumidity = (double)cache.PropertyCache.Properties[RoomHumidityName].Value;

            if (officeTemperature <= MinimumOfficeTemperature) {
                heater.State = 1;
                floorHeating.State = 1;
            } else {
                heater.State = 0;
                floorHeating.State = 0;
            }

            if (officeHumidity >= MaximumOfficeHumidity) {
                dehumidifier.State = 1;
            } else {
                dehumidifier.State = 0;
            }

            // Although we plan Actions with a simulation, we can't estimate their outcomes like we can with the ruleless method. The cache will thus remain as it is.
            var simulation = new Simulation(cache.PropertyCache) {
                Actions = new List<Models.OntologicalModels.Action>() {
                    new ActuationAction {
                        Name = "Heater_" + heater.State!.ToString(),
                        Actuator = heater,
                        NewStateValue = heater.State
                    },
                    new ActuationAction {
                        Name = "FloorHeating_" + floorHeating.State!.ToString(),
                        Actuator = floorHeating,
                        NewStateValue = floorHeating.State
                    },
                    new ActuationAction {
                        Name = "Dehumidifier_" + dehumidifier.State!.ToString(),
                        Actuator = dehumidifier,
                        NewStateValue = dehumidifier.State
                    }
                }
            };

            _logger.LogInformation("Generated decision.");

            return simulation;
        }
    }
}
