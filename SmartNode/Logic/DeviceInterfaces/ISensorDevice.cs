namespace Logic.DeviceInterfaces
{
    public interface ISensorDevice
    {
        public string SensorName { get; init; }

        public string ProcedureName { get; init; }

        /// <summary>
        /// Observes the relevant property's value. This assumes that Sensors will not handle different Input
        /// and Output types.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="inputs"></param>
        /// <returns></returns>
        /// 
        /// This is just one way of handling observation. We could also consider a publish-subscribe pattern.
        public object ObservePropertyValue(params object[] inputs);
    }
}
