using Logic.DeviceInterfaces;
using Logic.SensorValueHandlers;

namespace Logic.FactoryInterface
{
    public interface IFactory
    {
        public ISensorDevice GetSensorDeviceImplementation(string sensorName, string procedureName);

        public IActuatorDevice GetActuatorDeviceImplementation(string actuatorName);

        public ISensorValueHandler GetSensorValueHandlerImplementation(string owlType);
    }
}
