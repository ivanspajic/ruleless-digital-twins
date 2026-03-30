using Logic.TTComponentInterfaces;

namespace Implementations.Sensors.RoomM370 {
    public class MotionSensor : ISensor {
        public MotionSensor(string sensorName, string procedureName) {
            SensorName = sensorName;
            ProcedureName = procedureName;
        }

        public string SensorName { get; private set; }

        public string ProcedureName { get; private set; }

        public async Task<object> ObservePropertyValue(params object[] inputProperties) {
            return true;
        }
    }
}
