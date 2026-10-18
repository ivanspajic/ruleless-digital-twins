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
                    "http://www.semanticweb.org/ispa/ontologies/tam-scht/HVAC",
                    new HvacActuator("http://www.semanticweb.org/ispa/ontologies/tam-scht/HVAC")
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
                },
                #endregion
#region Weather Sensors
                {
                    ("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_altSensor",
                    "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_altProcedure"),
                    new DummyWeatherSensor("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_altSensor",
                        "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_altProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_ceiHeiSensor",
                    "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_ceiHeiProcedure"),
                    new DummyWeatherSensor("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_ceiHeiSensor",
                        "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_ceiHeiProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_cloTimSensor",
                    "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_cloTimProcedure"),
                    new DummyWeatherSensor("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_cloTimSensor",
                        "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_cloTimProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_HDifHorSensor",
                    "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_HDifHorProcedure"),
                    new DummyWeatherSensor("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_HDifHorSensor",
                        "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_HDifHorProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_HDirNorSensor",
                    "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_HDirNorProcedure"),
                    new DummyWeatherSensor("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_HDirNorSensor",
                        "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_HDirNorProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_HGloHorSensor",
                    "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_HGloHorProcedure"),
                    new DummyWeatherSensor("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_HGloHorSensor",
                        "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_HGloHorProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_HHorIRSensor",
                    "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_HHorIRProcedure"),
                    new DummyWeatherSensor("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_HHorIRSensor",
                        "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_HHorIRProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_latSensor",
                    "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_latProcedure"),
                    new DummyWeatherSensor("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_latSensor",
                        "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_latProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_lonSensor",
                    "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_lonProcedure"),
                    new DummyWeatherSensor("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_lonSensor",
                        "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_lonProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_nOpaSensor",
                    "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_nOpaProcedure"),
                    new DummyWeatherSensor("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_nOpaSensor",
                        "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_nOpaProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_nTotSensor",
                    "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_nTotProcedure"),
                    new DummyWeatherSensor("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_nTotSensor",
                        "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_nTotProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_pAtmSensor",
                    "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_pAtmProcedure"),
                    new DummyWeatherSensor("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_pAtmSensor",
                        "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_pAtmProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_relHumSensor",
                    "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_relHumProcedure"),
                    new DummyWeatherSensor("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_relHumSensor",
                        "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_relHumProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_solAltSensor",
                    "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_solAltProcedure"),
                    new DummyWeatherSensor("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_solAltSensor",
                        "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_solAltProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_solDecSensor",
                    "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_solDecProcedure"),
                    new DummyWeatherSensor("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_solDecSensor",
                        "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_solDecProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_solHouAngSensor",
                    "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_solHouAngProcedure"),
                    new DummyWeatherSensor("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_solHouAngSensor",
                        "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_solHouAngProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_solTimSensor",
                    "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_solTimProcedure"),
                    new DummyWeatherSensor("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_solTimSensor",
                        "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_solTimProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_solZenSensor",
                    "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_solZenProcedure"),
                    new DummyWeatherSensor("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_solZenSensor",
                        "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_solZenProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_TBlaSkySensor",
                    "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_TBlaSkyProcedure"),
                    new DummyWeatherSensor("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_TBlaSkySensor",
                        "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_TBlaSkyProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_TDewPoiSensor",
                    "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_TDewPoiProcedure"),
                    new DummyWeatherSensor("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_TDewPoiSensor",
                        "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_TDewPoiProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_TDryBulSensor",
                    "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_TDryBulProcedure"),
                    new DummyWeatherSensor("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_TDryBulSensor",
                        "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_TDryBulProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_TWetBulSensor",
                    "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_TWetBulProcedure"),
                    new DummyWeatherSensor("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_TWetBulSensor",
                        "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_TWetBulProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_winDirSensor",
                    "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_winDirProcedure"),
                    new DummyWeatherSensor("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_winDirSensor",
                        "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_winDirProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_winSpeSensor",
                    "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_winSpeProcedure"),
                    new DummyWeatherSensor("http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_winSpeSensor",
                        "http://www.semanticweb.org/ispa/ontologies/tam-scht/fmu_winSpeProcedure")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/tam-scht/HouseTempSensor",
                    "http://www.semanticweb.org/ispa/ontologies/tam-scht/HouseTempProcedure"),
                    new DummyWeatherSensor("http://www.semanticweb.org/ispa/ontologies/tam-scht/HouseTempSensor",
                        "http://www.semanticweb.org/ispa/ontologies/tam-scht/HouseTempProcedure")
                }
#endregion
            };
        }
    }
}
