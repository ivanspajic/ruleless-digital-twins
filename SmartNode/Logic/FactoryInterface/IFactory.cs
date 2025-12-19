using Logic.TTComponentInterfaces;
using Logic.ValueHandlerInterfaces;

namespace Logic.FactoryInterface
{
    public interface IFactory
    {
        public ISensorDevice GetSensorDeviceImplementation(string sensorName, string procedureName);

        public IActuatorDevice GetActuatorDeviceImplementation(string actuatorName);

        public IValueHandler GetValueHandlerImplementation(string owlType);
    }
}
