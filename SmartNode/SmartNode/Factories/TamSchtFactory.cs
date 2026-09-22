using Implementations.Actuators.TAM_SCHT;
using Implementations.Sensors.TAM_SCHT;
using Logic.FactoryInterface;
using Logic.TTComponentInterfaces;

namespace SmartNode.Factories {
    public class TamSchtFactory : AbstractFactory, IFactory {
        public TamSchtFactory(IServiceProvider serviceProvider) : base(serviceProvider) { }

        protected override IDictionary<string, IActuator> MakeActuatorMap(IServiceProvider serviceProvider) {
            return new Dictionary<string, IActuator> {
                {
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/HVAC",
                    new HvacActuator("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/HVAC")
                }
            };
        }

        protected override IDictionary<string, IConfigurableParameter> MakeConfigurableParameterMap(IServiceProvider serviceProvider) {
            return new Dictionary<string, IConfigurableParameter>();
        }

        protected override IDictionary<(string, string), ISensor> MakeSensorMap(IServiceProvider serviceProvider) {
            return new Dictionary<(string, string), ISensor>() {
#region Thermocouples
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/BathSpaceThermocouple",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/BathSpaceProcedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/BathSpaceThermocouple",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/BathSpaceProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/BathSurfaceThermocouple",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/BathSurfaceProcedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/BathSurfaceThermocouple",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/BathSurfaceProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/Bedroom2SpaceThermocouple",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/Bedroom2SpaceProcedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/Bedroom2SpaceThermocouple",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/Bedroom2SpaceProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/Bedroom2SurfaceThermocouple",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/Bedroom2SurfaceProcedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/Bedroom2SurfaceThermocouple",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/Bedroom2SurfaceProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/Bedroom3SpaceThermocouple",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/Bedroom3SpaceProcedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/Bedroom3SpaceThermocouple",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/Bedroom3SpaceProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/Bedroom3SurfaceThermocouple",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/Bedroom3SurfaceProcedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/Bedroom3SurfaceThermocouple",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/Bedroom3SurfaceProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/KitchenSpaceThermocouple",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/KitchenSpaceProcedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/KitchenSpaceThermocouple",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/KitchenSpaceProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/KitchenWindow1SurfaceThermocouple",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/KitchenWindow1SurfaceProcedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/KitchenWindow1SurfaceThermocouple",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/KitchenWindow1SurfaceProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/KitchenWindow2SurfaceThermocouple",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/KitchenWindow2SurfaceProcedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/KitchenWindow2SurfaceThermocouple",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/KitchenWindow2SurfaceProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/LivingRoomSpace1Thermocouple",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/LivingRoomSpace1Procedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/LivingRoomSpace1Thermocouple",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/LivingRoomSpace1Procedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/LivingRoomSpace2Thermocouple",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/LivingRoomSpace2Procedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/LivingRoomSpace2Thermocouple",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/LivingRoomSpace2Procedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/LivingRoomSurface1Thermocouple",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/LivingRoomSurface1Procedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/LivingRoomSurface1Thermocouple",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/LivingRoomSurface1Procedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/LivingRoomSurface2Thermocouple",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/LivingRoomSurface2Procedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/LivingRoomSurface2Thermocouple",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/LivingRoomSurface2Procedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBathSpaceThermocouple",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBathSpaceProcedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBathSpaceThermocouple",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBathSpaceProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBathSurfaceThermocouple",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBathSurfaceProcedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBathSurfaceThermocouple",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBathSurfaceProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBedroomSpace1Thermocouple",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBedroomSpace1Procedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBedroomSpace1Thermocouple",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBedroomSpace1Procedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBedroomSpace2Thermocouple",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBedroomSpace2Procedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBedroomSpace2Thermocouple",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBedroomSpace2Procedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBedroomSurfaceThermocouple",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBedroomSurfaceProcedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBedroomSurfaceThermocouple",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBedroomSurfaceProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/UtilitySpaceThermocouple",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/UtilitySpaceProcedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/UtilitySpaceThermocouple",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/UtilitySpaceProcedure")
                },
#endregion
#region Humidity Sensors
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/BathHumiditySensor",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/BathHumidityProcedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/BathHumiditySensor",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/BathHumidityProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/Bedroom2HumiditySensor",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/Bedroom2HumidityProcedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/Bedroom2HumiditySensor",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/Bedroom2HumidityProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/Bedroom3HumiditySensor",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/Bedroom3HumidityProcedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/Bedroom3HumiditySensor",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/Bedroom3HumidityProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/KitchenHumiditySensor",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/KitchenHumidityProcedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/KitchenHumiditySensor",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/KitchenHumidityProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/LivingRoomSpace1HumiditySensor",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/LivingRoomSpace1HumidityProcedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/LivingRoomSpace1HumiditySensor",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/LivingRoomSpace1HumidityProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/LivingRoomSpace2HumiditySensor",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/LivingRoomSpace2HumidityProcedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/LivingRoomSpace2HumiditySensor",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/LivingRoomSpace2HumidityProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBathHumiditySensor",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBathHumidityProcedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBathHumiditySensor",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBathHumidityProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBedroomSpace1HumiditySensor",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBedroomSpace1HumidityProcedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBedroomSpace1HumiditySensor",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBedroomSpace1HumidityProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBedroomSpace2HumiditySensor",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBedroomSpace2HumidityProcedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBedroomSpace2HumiditySensor",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBedroomSpace2HumidityProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/UtilityHumiditySensor",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/UtilityHumidityProcedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/UtilityHumiditySensor",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/UtilityHumidityProcedure")
                },
#endregion
#region Radiant Sensors
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/KitchenRadiantSensor",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/KitchenRadiantProcedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/KitchenRadiantSensor",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/KitchenRadiantProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/LivingRoomRadiantSensor",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/LivingRoomRadiantProcedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/LivingRoomRadiantSensor",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/LivingRoomRadiantProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBedroomRadiantSensor",
                    "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBedroomRadiantProcedure"),
                    new Thermocouple("http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBedroomRadiantSensor",
                        "http://www.semanticweb.org/ispa/ontologies/2026/5/untitled-ontology-515/PrimaryBedroomRadiantProcedure")
                }
#endregion
            };
        }
    }
}
