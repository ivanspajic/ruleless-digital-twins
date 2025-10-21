using Logic.DeviceInterfaces;

namespace Implementations.Sensors
{
    public class DummySensor : ISensorDevice
    {
        public DummySensor(string sensorName, string procedureName)
        {

        }

        public string SensorName => throw new NotImplementedException();

        public string ProcedureName => throw new NotImplementedException();

        public object ObservePropertyValue(params object[] inputProperties) => throw new NotImplementedException();
    }
}
