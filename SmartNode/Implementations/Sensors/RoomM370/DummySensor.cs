using Logic.TTComponentInterfaces;

namespace Implementations.Sensors.RoomM370
{
    public class DummySensor(string sensorName, string procedureName) : ISensor
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
