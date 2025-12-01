using Logic.DeviceInterfaces;

namespace Implementations.Sensors
{
    public class DummySensor : ISensorDevice
    {
        public DummySensor(string sensorName, string procedureName)
        {
            SensorName = sensorName;
            ProcedureName = procedureName;
        }

        public string SensorName { get; private set; }

        public string ProcedureName { get; private set; }

        public virtual object ObservePropertyValue(params object[] inputProperties) => throw new NotImplementedException();
    }

    public class ConstantSensor : DummySensor
    {
        public ConstantSensor(string sensorName, string procedureName, double value) : base(sensorName, procedureName)
        {
            Value = value;
        }

        public double Value { get; private set; }
        public override object ObservePropertyValue(params object[] inputProperties) => Value;
    }
}
