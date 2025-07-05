using Logic.DeviceInterfaces;
using Logic.FactoryInterface;
using Logic.SensorValueHandlers;
using SensorActuatorImplementations.Actuators;
using SensorActuatorImplementations.Sensors;
using SensorActuatorImplementations.ValueHandlers;

namespace SmartNode
{
    internal class Factory : IFactory
    {
        // New implementations can simply be added to the factory collections.
        private readonly Dictionary<(string, string), ISensorDevice> _sensors = new()
        {
            {
                ("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#SoftSensor1",
                "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#SoftSensor1Algorithm"), new ExampleSensor
                {
                    ProcedureName = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#SoftSensor1Algorithm",
                    SensorName = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#SoftSensor1"
                }
            },
            {
                ("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor2",
                "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor2Procedure"), new ExampleSensor
                {
                    ProcedureName = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor2Procedure",
                    SensorName = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor2"
                }
            },
            {
                ("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor1",
                "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor1Procedure"), new ExampleSensor
                {
                    ProcedureName = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor1Procedure",
                    SensorName = "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor1"
                }
            },
            {
                ("http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CustomPieceSoftSensor",
                "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CompressionRatioAlgorithm"), new ExampleSensor
                {
                    ProcedureName = "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CompressionRatioAlgorithm",
                    SensorName = "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CustomPieceSoftSensor"
                }
            },
            {
                ("http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CustomPieceSoftSensor",
                "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CustomPiece"), new ExampleSensor
                {
                    ProcedureName = "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CustomPiece",
                    SensorName = "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CustomPieceSoftSensor"
                }
            },
            {
                ("http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/TemperatureSensor1",
                "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/TemperatureSensor1Procedure"), new ExampleSensor
                {
                    ProcedureName = "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/TemperatureSensor1Procedure",
                    SensorName = "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/TemperatureSensor1"
                }
            }
        };
        private readonly Dictionary<string, IActuatorDevice> _actuators = new()
        {
            {
                "Heater", new ExampleActuator()
                {
                    ActuatorName = "Heater"
                }
            }
        };

        // The keys represent the OWL (RDF/XSD) types supported by Protege, and the values are user implementations.
        private readonly Dictionary<string, IValueHandler> _sensorValueHandlers = new()
        {
            { "double", new ExampleDoubleValueHandler() },
            { "int", new ExampleIntValueHandler() }
        };

        public ISensorDevice GetSensorDeviceImplementation(string sensorName, string procedureName)
        {
            if (_sensors.TryGetValue((sensorName, procedureName), out ISensorDevice sensor))
            {
                return sensor;
            }

            throw new Exception($"No implementation was found for Sensor {sensorName} with Procedure {procedureName}.");
        }

        public IActuatorDevice GetActuatorDeviceImplementation(string actuatorName)
        {
            if (_actuators.TryGetValue(actuatorName, out IActuatorDevice actuator))
            {
                return actuator;
            }

            throw new Exception($"No implementation was found for Actuator {actuator}.");
        }

        public IValueHandler GetSensorValueHandlerImplementation(string owlType)
        {
            if (_sensorValueHandlers.TryGetValue(owlType, out IValueHandler sensorValueHandler))
            {
                return sensorValueHandler;
            }

            throw new Exception($"No implementation was found for Sensor value handler for OWL type {owlType}.");
        }
    }
}
