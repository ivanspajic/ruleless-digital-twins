using Logic.DeviceInterfaces;

namespace SensorActuatorImplementations
{
    public class ExampleSensorDoubleValues : ISensor<double>
    {
        public string Name { get => throw new NotImplementedException(); init => throw new NotImplementedException(); }

        public double ObservePropertyValue()
        {
            throw new NotImplementedException();
        }
    }
}
