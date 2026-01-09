using Logic.TTComponentInterfaces;
using Logic.FactoryInterface;
using Logic.ValueHandlerInterfaces;
using TestProject.Mocks.ValueHandlerMocks;

namespace TestProject.Mocks.ServiceMocks
{
    internal class FactoryMock : IFactory
    {
        private Dictionary<string, IValueHandler> _valueHandlerImplementations = new()
        {
            { "double", new DoubleValueHandlerMock() },
            { "int", new IntValueHandlerMock() }
        };

        public IActuator GetActuatorDeviceImplementation(string actuatorName)
        {
            throw new NotImplementedException();
        }

        public IConfigurableParameter GetConfigurableParameterImplementation(string configurableParameterName) {
            throw new NotImplementedException();
        }

        public ISensor GetSensorDeviceImplementation(string sensorName, string procedureName)
        {
            throw new NotImplementedException();
        }

        public IValueHandler GetValueHandlerImplementation(string owlType)
        {
            if (_valueHandlerImplementations.TryGetValue(owlType, out IValueHandler? valueHandler))
            {
                return valueHandler;
            }

            return null!;
        }

        public void AddValueHandlerImplementation(string owlType, IValueHandler valueHandler) {
            _valueHandlerImplementations.Add(owlType, valueHandler);
        }
    }
}
