using Logic.TTComponentInterfaces;
using Logic.FactoryInterface;
using Logic.ValueHandlerInterfaces;
using Implementations.ValueHandlers;
using Implementations.Sensors.RoomM370;
using Implementations.Sensors.CustomPiece;
using Implementations.SoftwareComponents;
using Implementations.Actuators.RoomM370;
using Implementations.Sensors.Incubator;
using RabbitMQ.Client;
using Implementations.Actuators.Incubator;

namespace SmartNode
{
    internal class Factory : IFactory
    {
        private readonly string _environment;

        // Since sensors and actuators mostly relate to sensor-actuator networks as communciation media for physical TTs (PTs), this factory allows for registering implementations
        // that deliberately do not use the physical implementation as the TT. For testing purposes, one can thus register sensors and actuators for mock environments (dummy
        // environments) with the names of those environments as keys of the maps. Since ConfigurableParameters and value handlers aren't coupled to physical systems, these can just
        // be registered in one map.
        // 
        // New implementations can simply be added to the factory collections.
        private readonly Dictionary<string, SensorActuatorMapWrapper> _sensorActuatorMaps = new() {
            {
                "incubator",
                new SensorActuatorMapWrapper {
                    ActuatorMap = new() {
                        {
                            "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#HeaterActuator",
                            new AmqHeater()
                        }
                    },
                    SensorMap = new() {
                        {
                            ("http://www.semanticweb.org/vs/ontologies/2025/12/incubator#TempSensor",
                            "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#TempProcedure"),
                            new AmqSensor("http://www.semanticweb.org/vs/ontologies/2025/12/incubator#TempSensor",
                                "http://www.semanticweb.org/vs/ontologies/2025/12/incubator#TempProcedure",
                                d => d.average_temperature)
                        }
                    }
                }
            },
            {
                "roomM370",
                new SensorActuatorMapWrapper {
                    ActuatorMap = new() {
                        {
                            "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater",
                            new DummyHeater("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Heater")
                        },
                        {
                            "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating",
                            new DummyHeater("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#FloorHeating")
                        },
                        {
                            "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier",
                            new DummyDehumidifier("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier")
                        }
                    },
                    SensorMap = new() {
                        {
                            ("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#SoftSensor1",
                            "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#SoftSensor1Algorithm"),
                            new DummyTemperatureSensor("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#SoftSensor1Algorithm",
                                "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#SoftSensor1")
                        },
                        {
                            ("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor2",
                            "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor2Procedure"),
                            new DummyTemperatureSensor("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor2Procedure",
                                "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor2")
                        },
                        {
                            ("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor1",
                            "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor1Procedure"),
                            new DummyTemperatureSensor("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor1Procedure",
                                "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor1")
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
                                "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/TemperatureSensor1")
                        },
                        {
                            ("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyConsumptionMeter",
                            "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyConsumptionMeterProcedure"),
                            new DummyEnergyConsumptionSensor("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyConsumptionMeterProcedure",
                                "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyConsumptionMeter")
                        },
                        {
                            ("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#HumiditySensor",
                            "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#HumiditySensorProcedure"),
                            new DummyHumiditySensor("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#HumiditySensorProcedure",
                                "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#HumiditySensor")
                        },
                        { // [VS] Abuse:
                            ("http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#DummySensor",
                            "http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#DummyProcedure"),
                            new ConstantSensor("http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#DummyProcedure",
                                "http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#DummySensor", -1)
                        },
                        {
                            ("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#IncreaseTemperatureSoftSensor",
                            "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#IncreaseTemperatureSoftSensorProcedure"),
                            new IncreaseTemperatureSoftSensor("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#IncreaseTemperatureSoftSensor",
                                "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#IncreaseTemperatureSoftSensorProcedure")
                        },
                        {
                            ("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#DecreaseTemperatureSoftSensor",
                            "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#DecreaseTemperatureSoftSensorProcedure"),
                            new DecreaseTemperatureSoftSensor("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#DecreaseTemperatureSoftSensor",
                                "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#DecreaseTemperatureSoftSensorProcedure")
                        },
                        {
                            ("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AverageTemperatureSoftSensor",
                            "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AverageTemperatureSoftSensorProcedure"),
                            new AverageTemperatureSoftSensor("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AverageTemperatureSoftSensor",
                                "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AverageTemperatureSoftSensorProcedure")
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
                        }
                    }
                }
            },
            {
                string.Empty,
                new SensorActuatorMapWrapper {
                    ActuatorMap = new() {

                    },
                    SensorMap = new() {

                    }
                }
            }
        };

        private readonly Dictionary<string, IConfigurableParameter> _configurableParameters = new() {
            {
                "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/BucketSize",
                new DummyConfigurableParameter("http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/BucketSize")
            },
            {
                "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/Epsilon",
                new DummyConfigurableParameter("http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/Epsilon")
            }
        };

        // The keys represent the OWL (RDF/XSD) types supported by Protege, and the values are user implementations.
        private readonly Dictionary<string, IValueHandler> _valueHandlers = new() {
            { "http://www.w3.org/2001/XMLSchema#double", new DoubleValueHandler() },
            { "http://www.w3.org/2001/XMLSchema#int", new IntValueHandler() },
            { "http://www.w3.org/2001/XMLSchema#base64Binary", new Base64BinaryValueHandler() }
        };

        public Factory(string dummyEnvironment) {
            _environment = dummyEnvironment;
        }

        public ISensor GetSensorImplementation(string sensorName, string procedureName) {
            if (_sensorActuatorMaps.TryGetValue(_environment, out SensorActuatorMapWrapper? sensorActuatorMapWrapper)) {
                if (sensorActuatorMapWrapper.SensorMap.TryGetValue((sensorName, procedureName), out ISensor? sensor)) {
                    return sensor;
                }

                throw new Exception($"No implementation was found for Sensor {sensorName} with Procedure {procedureName}.");
            }

            throw new Exception($"No sensor-actuator mapping exists for environment {_environment}.");
        }

        public IActuator GetActuatorImplementation(string actuatorName) {
            if (_sensorActuatorMaps.TryGetValue(_environment, out SensorActuatorMapWrapper? sensorActuatorMapWrapper)) {
                if (sensorActuatorMapWrapper.ActuatorMap.TryGetValue(actuatorName, out IActuator? actuator)) {
                    return actuator;
                }

                throw new Exception($"No implementation was found for Actuator {actuatorName}.");
            }

            throw new Exception($"No sensor-actuator mapping exists for environment {_environment}.");
        }

        public IConfigurableParameter GetConfigurableParameterImplementation(string configurableParameterName) {
            if (_configurableParameters.TryGetValue(configurableParameterName, out IConfigurableParameter? configurableParameter)) {
                return configurableParameter;
            }

            throw new Exception($"No implementation was found for software component {configurableParameterName}.");
        }

        public IValueHandler GetValueHandlerImplementation(string owlType) {
            if (_valueHandlers.TryGetValue(owlType, out IValueHandler? sensorValueHandler)) {
                return sensorValueHandler;
            }

            throw new Exception($"No implementation was found for Sensor value handler for OWL type {owlType}.");
        }

        private class SensorActuatorMapWrapper {
            public required Dictionary<(string, string), ISensor> SensorMap { get; set; }

            public required Dictionary<string, IActuator> ActuatorMap { get; set; }
        }
    }
}
