using Logic.TTComponentInterfaces;

namespace Implementations.Sensors.RoomM370 {
    public class PricePerEnergySoftSensor : ISensor {
        public PricePerEnergySoftSensor(string sensorName, string procedureName, Func<double, double, double> func) {
            SensorName = sensorName;
            ProcedureName = procedureName;
            Func = func;
        }

        public string SensorName { get; }

        public string ProcedureName { get; }
        public Func<double, double, double> Func{ get; }

        public async Task<object> ObservePropertyValue(params object[] inputProperties) {
            return Func((double)inputProperties[0], (double)inputProperties[1]); // XXX Review
        }
    }
}
