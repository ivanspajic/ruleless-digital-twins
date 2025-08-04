using Logic.DeviceInterfaces;
using Logic.FactoryInterface;
using Logic.ValueHandlerInterfaces;

namespace TestProject
{
    internal class FactoryMock : IFactory
    {
        public IActuatorDevice GetActuatorDeviceImplementation(string actuatorName)
        {
            throw new NotImplementedException();
        }

        public ISensorDevice GetSensorDeviceImplementation(string sensorName, string procedureName)
        {
            throw new NotImplementedException();
        }

        public IValueHandler GetValueHandlerImplementation(string owlType)
        {
            // TODO: implement a mock of this!!
            return null;
        }
    }
}
