using Logic.DeviceInterfaces;
using Logic.FactoryInterface;
using Logic.SensorValueHandlers;
using SensorActuatorImplementations;

namespace SmartNode
{
    internal class Factory : IFactory
    {
        // New implementations can simply be added to the factory collections.
        private readonly Dictionary<(string, string), ISensor> _sensors = new()
        {
            {
                ("test", "test"), new ExampleSensor
                {
                    ProcedureName = "test",
                    SensorName = "test"
                }
            }
        };

        private readonly Dictionary<string, ISensorValueHandler> _sensorValueHandlers = new()
        {
            { "double", new SensorDoubleValueHandler() },
            { "int", new SensorIntValueHandler() }
        };

        public ISensor GetSensorImplementation(string sensorName, string procedureName)
        {
            if (_sensors.TryGetValue((sensorName, procedureName), out ISensor sensor))
                return sensor;

            throw new Exception($"No implementation was found for Sensor {sensorName} with Procedure {procedureName}.");
        }

        public ISensorValueHandler GetSensorValueHandlerImplementation(string owlType)
        {
            if (_sensorValueHandlers.TryGetValue(owlType, out ISensorValueHandler sensorValueHandler))
                return sensorValueHandler;

            throw new Exception($"No implementation was found for Sensor value handler for OWL type {owlType}.");
        }
    }
}
