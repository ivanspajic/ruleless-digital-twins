using Implementations.Actuators.RoomM370;
using Implementations.Sensors.CustomPiece;
using Implementations.Sensors.Fakepool;
using Implementations.Sensors.RoomM370;
using Implementations.SimulatedTwinningTargets;
using Implementations.SoftwareComponents;
using Logic.FactoryInterface;
using Logic.TTComponentInterfaces;

namespace SmartNode.Factories {
    public class RoomM370Factory : AbstractFactory, IFactory {
        private static DummyRoomM370? _dummyRoomM370;

        public RoomM370Factory(IServiceProvider serviceProvider) : this(Wrapper(serviceProvider)) { }

        private RoomM370Factory(Wrapped w) : base(w.ServiceProvider) { }

        private static Wrapped Wrapper(IServiceProvider serviceProvider) {
            // Make sure that we always have the Incubator initialised.
            // Inspired by https://stackoverflow.com/q/12051/60462
            EnsureDummyRoomM370Instance(serviceProvider);
            return new Wrapped(serviceProvider);
        }

        private readonly record struct Wrapped(IServiceProvider ServiceProvider);

        protected override IDictionary<string, IActuator> MakeActuatorMap(IServiceProvider serviceProvider) {
            EnsureDummyRoomM370Instance(serviceProvider);

            return new Dictionary<string, IActuator> {
                {
                    "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater",
                    new DummyHeater("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater", _dummyRoomM370)
                },
                {
                    "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating",
                    new DummyFloorHeating("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating", _dummyRoomM370)
                },
                {
                    "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier",
                    new DummyDehumidifier("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier", _dummyRoomM370)
                },
                { // [VS] Abuse -- input for FMU which does not have a TT
                    "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FakepoolStepActuator",
                    new DummyDehumidifier("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FakepoolStepActuator", _dummyRoomM370)
                }
            };
        }

        protected override IDictionary<string, IConfigurableParameter> MakeConfigurableParameterMap(IServiceProvider serviceProvider) {
            EnsureDummyRoomM370Instance(serviceProvider);

            return new Dictionary<string, IConfigurableParameter> {
                {
                "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/BucketSize",
                new DummyConfigurableParameter("http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/BucketSize")
                },
                {
                    "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/Epsilon",
                    new DummyConfigurableParameter("http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/Epsilon")
                }
            };
        }

        protected override IDictionary<(string, string), ISensor> MakeSensorMap(IServiceProvider serviceProvider) {
            EnsureDummyRoomM370Instance(serviceProvider);

            return new Dictionary<(string, string), ISensor>() {
                {
                    ("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#SoftSensor1",
                    "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#SoftSensor1Algorithm"),
                    new DummyTemperatureSensor("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#SoftSensor1Algorithm",
                        "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#SoftSensor1",
                        _dummyRoomM370)
                },
                {
                    ("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor2",
                    "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor2Procedure"),
                    new DummyTemperatureSensor("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor2Procedure",
                        "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor2",
                        _dummyRoomM370)
                },
                {
                    ("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor1",
                    "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor1Procedure"),
                    new DummyTemperatureSensor("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor1Procedure",
                        "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor1",
                        _dummyRoomM370)
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CustomPieceSoftSensor",
                    "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CompressionRatioAlgorithm"),
                    new DummySensor("http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CompressionRatioAlgorithm",
                        "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CustomPieceSoftSensor")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CustomPieceSoftSensor",
                    "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CustomPiece"),
                    new DummySensor("http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CustomPiece",
                        "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CustomPieceSoftSensor")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/TemperatureSensor1",
                    "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/TemperatureSensor1Procedure"),
                    new DummyTemperatureSensor("http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/TemperatureSensor1Procedure",
                        "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/TemperatureSensor1",
                        _dummyRoomM370)
                },
                {
                    ("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyConsumptionMeter",
                    "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyConsumptionMeterProcedure"),
                    new DummyEnergyConsumptionSensor("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyConsumptionMeterProcedure",
                        "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyConsumptionMeter",
                        _dummyRoomM370)
                },
                {
                    ("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#HumiditySensor",
                    "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#HumiditySensorProcedure"),
                    new DummyHumiditySensor("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#HumiditySensorProcedure",
                        "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#HumiditySensor",
                        _dummyRoomM370)
                },
                { // [VS] Abuse:
                    ("http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#DummySensor",
                    "http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#DummyProcedure"),
                    new ConstantSensor("http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#DummyProcedure",
                        "http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#DummySensor", -1)
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CompressionRatioSoftSensor",
                    "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CompressionRatioAlgorithm"),
                    new CompressionRatioSoftSensor("http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CompressionRatioSoftSensor",
                        "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CompressionRatioAlgorithm")
                },
                {
                    ("http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CustomPieceSoftSensor",
                    "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CustomPieceAlgorithm"),
                    new CustomPieceSoftSensor("http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CustomPieceSoftSensor",
                        "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CustomPieceAlgorithm")
                },
                {
                    ("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#MotionSensor",
                    "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#MotionSensorProcedure"),
                    new MotionSensor("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#MotionSensor",
                        "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#MotionSensorProcedure")
                },
                {
                    ("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#PriceSensor",
                    "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#PriceProcedure"),
                    new FakepoolSensor("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#PriceSensor",
                        "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#PriceProcedure")
                },
                {
                    ("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#PriceDummySensor",
                    "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#PriceDummyProcedure"),
                    new ConstantSensor("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#PriceDummySensor",
                        "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#PriceDummyProcedure", 1.58) // XXX In the absence of an FP-not-so-softsensor

                },
                {
                    ("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#PricePerEnergySoftSensor",
                    "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#PricePerEnergyProcedure"),
                    new PricePerEnergySoftSensor("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#PricePerEnergySoftSensor",
                        "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#PricePerEnergyProcedure", (x,y) => x*y)
                },



                // The following are workarounds due to a bug in how we query Inputs/Outputs and build soft sensor trees!
                {
                    ("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FakepoolNotFoundSensor",
                    "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FakepoolNotFoundProcedure"),
                    new GeneralConstantSensor("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FakepoolNotFoundSensor",
                        "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FakepoolNotFoundProcedure",
                        false)
                },
                {
                    ("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FakepoolStepSensor",
                    "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FakepoolStepProcedure"),
                    new GeneralConstantSensor("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FakepoolStepSensor",
                        "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FakepoolStepProcedure",
                        0.0)
                },
                {
                    ("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#MapekCycleSensor",
                    "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#MapekCycleProcedure"),
                    new GeneralConstantSensor("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#MapekCycleSensor",
                        "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#MapekCycleProcedure",
                        0)
                }
            };
        }

        private static void EnsureDummyRoomM370Instance(IServiceProvider serviceProvider) {
            _dummyRoomM370 ??= new DummyRoomM370(serviceProvider);
        }
    }
}
