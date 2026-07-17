using Logic.TTComponentInterfaces;

namespace Implementations.Sensors.TAM_SCHT {
    public class RadiantSensor : ISensor {
        public RadiantSensor(string sensorName, string procedureName) {
            SensorName = sensorName;
            ProcedureName = procedureName;
        }

        public string SensorName { get; private set; }

        public string ProcedureName { get; private set; }

        public async Task<object> ObservePropertyValue(params object[] inputProperties) {
            return 1.0;
        }
    }
}
