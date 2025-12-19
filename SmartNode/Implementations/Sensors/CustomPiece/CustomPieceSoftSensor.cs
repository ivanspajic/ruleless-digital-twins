using Logic.TTComponentInterfaces;

namespace Implementations.Sensors.CustomPiece {
    public class CustomPieceSoftSensor : ISensor {
        public CustomPieceSoftSensor(string sensorName, string procedureName) {
            SensorName = sensorName;
            ProcedureName = procedureName;
        }

        public string SensorName { get; private set; }

        public string ProcedureName { get; private set; }

        public object ObservePropertyValue(params object[] inputProperties) {
            return 1; // TODO: return the CustomPiece/MixPiece compressed time series object.
        }
    }
}
