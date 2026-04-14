using Logic.TTComponentInterfaces;

namespace Implementations.Sensors.RoomM370 {
    public class PriceSensor : ISensor {
        public PriceSensor(string sensorName, string procedureName) {
            SensorName = sensorName;
            ProcedureName = procedureName;
        }

        public string SensorName { get; private set; }

        public string ProcedureName { get; private set; }

        public async Task<object> ObservePropertyValue(params object[] inputProperties) {
            return 1.56; // Dummy price.
        }
    }
}
