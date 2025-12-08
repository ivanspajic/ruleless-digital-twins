using Logic.DeviceInterfaces;

namespace Implementations.Sensors
{
    public class DummySensor(string sensorName, string procedureName) : ISensorDevice
    {
        public string SensorName { get; private set; } = sensorName;

        public string ProcedureName { get; private set; } = procedureName;

        public virtual object ObservePropertyValue(params object[] inputProperties) => throw new NotImplementedException();
    }

    public class ConstantSensor(string sensorName, string procedureName, double value) : DummySensor(sensorName, procedureName)
    {
        public double Value { get; private set; } = value;
        public override object ObservePropertyValue(params object[] inputProperties) => Value;
    }
}
