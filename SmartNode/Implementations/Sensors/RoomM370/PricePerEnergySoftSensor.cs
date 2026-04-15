using Logic.TTComponentInterfaces;

namespace Implementations.Sensors.RoomM370 {
    public class PricePerEnergySoftSensor : ISensor {
        public PricePerEnergySoftSensor(string sensorName, string procedureName) {
            SensorName = sensorName;
            ProcedureName = procedureName;
        }

        public string SensorName { get; }

        public string ProcedureName { get; }

        public async Task<object> ObservePropertyValue(params object[] inputProperties) {
            return (double)inputProperties[0] * (double)inputProperties[1];
        }
    }
}
