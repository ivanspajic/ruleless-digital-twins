using Logic.TTComponentInterfaces;
using Logic.ValueHandlerInterfaces;

namespace Logic.FactoryInterface
{
    public interface IFactory
    {
        public ISensor GetSensorDeviceImplementation(string sensorName, string procedureName);

        public IActuator GetActuatorDeviceImplementation(string actuatorName);

        public IConfigurableParameter GetConfigurableParameterImplementation(string configurableParameterName);

        public IValueHandler GetValueHandlerImplementation(string owlType);
    }
}
