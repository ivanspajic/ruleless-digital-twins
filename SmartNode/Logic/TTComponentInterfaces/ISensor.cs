namespace Logic.TTComponentInterfaces
{
    public interface ISensor
    {
        public string SensorName { get; }

        public string ProcedureName { get; }

        public Task<object> ObservePropertyValue(params object[] inputProperties);
    }
}
