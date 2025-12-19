namespace Logic.TTComponentInterfaces
{
    public interface ISensorDevice
    {
        public string SensorName { get; }

        public string ProcedureName { get; }

        public object ObservePropertyValue(params object[] inputProperties);
    }
}
