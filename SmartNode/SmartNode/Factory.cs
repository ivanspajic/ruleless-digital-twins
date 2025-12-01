using Logic.DeviceInterfaces;
using Logic.FactoryInterface;
using Logic.ValueHandlerInterfaces;
using Implementations.Actuators;
using Implementations.Sensors;
using Implementations.ValueHandlers;
using Implementations.SimulatedTwinningTargets;

namespace SmartNode
{
    internal class Factory : IFactory
    {
        private readonly bool _useDummyDevices;

        // New implementations can simply be added to the factory collections.
        private readonly Dictionary<(string, string), ISensorDevice> _dummySensors = new()
        {
            {
                ("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#SoftSensor1",
                "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#SoftSensor1Algorithm"),
                new DummyTemperatureSensor(
                    "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#SoftSensor1Algorithm",
                    "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#SoftSensor1",
                    _dummyRoomM370)
            },
            {
                ("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor2",
                "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor2Procedure"),
                new DummyTemperatureSensor(
                    "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor2Procedure",
                    "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor2",
                    _dummyRoomM370)
            },
            {
                ("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor1",
                "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor1Procedure"),
                new DummyTemperatureSensor(
                    "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor1Procedure",
                    "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#TemperatureSensor1",
                    _dummyRoomM370)
            },
            {
                ("http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CustomPieceSoftSensor",
                "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CompressionRatioAlgorithm"),
                new DummySensor(
                    "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CompressionRatioAlgorithm",
                    "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CustomPieceSoftSensor")
            },
            {
                ("http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CustomPieceSoftSensor",
                "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CustomPiece"),
                new DummySensor(
                    "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CustomPiece",
                    "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/CustomPieceSoftSensor")
            },
            {
                ("http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/TemperatureSensor1",
                "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/TemperatureSensor1Procedure"),
                new DummyTemperatureSensor(
                    "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/TemperatureSensor1Procedure",
                    "http://www.semanticweb.org/ispa/ontologies/2025/instance-model-2/TemperatureSensor1",
                    _dummyRoomM370)
            },
            {
                ("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyConsumptionMeter",
                "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyConsumptionMeterProcedure"),
                new DummyEnergyConsumptionSensor(
                    "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyConsumptionMeterProcedure",
                    "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#EnergyConsumptionMeter",
                    _dummyRoomM370)
            },
            {
                ("http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#HumiditySensor",
                "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#HumiditySensorProcedure"),
                new DummyHumiditySensor(
                    "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#HumiditySensorProcedure",
                    "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#HumiditySensor",
                    _dummyRoomM370)
            },
            { // [VS] Abuse:
                ("http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#DummySensor",
                "http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#DummyProcedure"),
                new ConstantSensor(
                    "http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#DummyProcedure",
                    "http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#DummySensor", -1)
            }
        };

        private readonly Dictionary<string, IActuatorDevice> _dummyActuators = new()
        {
            {
                "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AirConditioningUnit",
                new DummyAirConditioningUnit(
                    "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#AirConditioningUnit",
                    _dummyRoomM370)
            },
            {
                "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier",
                new DummyDehumidifier(
                    "http://www.semanticweb.org/ivans/ontologies/2025/instance-model-1#Dehumidifier",
                    _dummyRoomM370)
            }
        };

        // The keys represent the OWL (RDF/XSD) types supported by Protege, and the values are user implementations.
        private readonly Dictionary<string, IValueHandler> _valueHandlers = new()
        {
            { "double", new DoubleValueHandler() },
            { "int", new IntValueHandler() }
        };

        // Keep an instance of the simulated TT.
        private static readonly DummyRoomM370 _dummyRoomM370 = new();

        public Factory(bool useDummyDevices)
        {
            _useDummyDevices = useDummyDevices;
        }

        public ISensorDevice GetSensorDeviceImplementation(string sensorName, string procedureName)
        {
            if (_useDummyDevices)
            {
                if (_dummySensors.TryGetValue((sensorName, procedureName), out ISensorDevice? sensor))
                {
                    return sensor;
                }
            }
            else
            {
                // Reserved for real implementations.
            }

            throw new Exception($"No implementation was found for Sensor {sensorName} with Procedure {procedureName}.");
        }

        public IActuatorDevice GetActuatorDeviceImplementation(string actuatorName)
        {
            if (_useDummyDevices)
            {
                if (_dummyActuators.TryGetValue(actuatorName, out IActuatorDevice? actuator))
                {
                    return actuator;
                }
            }
            else
            {
                // Reserved for real implementations.
            }

            throw new Exception($"No implementation was found for Actuator {actuatorName}.");
        }

        public IValueHandler GetValueHandlerImplementation(string owlType)
        {
            if (_valueHandlers.TryGetValue(owlType, out IValueHandler? sensorValueHandler))
            {
                return sensorValueHandler;
            }

            throw new Exception($"No implementation was found for Sensor value handler for OWL type {owlType}.");
        }
    }
}
