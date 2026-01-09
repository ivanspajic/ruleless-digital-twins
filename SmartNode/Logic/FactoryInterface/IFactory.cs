using Logic.TTComponentInterfaces;
using Logic.ValueHandlerInterfaces;

namespace Logic.FactoryInterface
{
    public interface IFactory
    {
        public ISensor GetSensorImplementation(string sensorName, string procedureName);

        public IActuator GetActuatorImplementation(string actuatorName);

        public IConfigurableParameter GetConfigurableParameterImplementation(string configurableParameterName);

        public IValueHandler GetValueHandlerImplementation(string owlType);
    }
}
