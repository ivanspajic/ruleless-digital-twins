using Logic.DeviceInterfaces;
using Logic.SensorValueHandlers;

namespace Logic.FactoryInterface
{
    public interface IFactory
    {
        public ISensor GetSensorImplementation(string sensorName, string procedureName);

        public IActuator GetActuatorImplementation(string actuatorName);

        public ISensorValueHandler GetSensorValueHandlerImplementation(string owlType);
    }
}
