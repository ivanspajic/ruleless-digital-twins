using Logic.TTComponentInterfaces;

namespace Implementations.Sensors.CustomPiece {
    public class CompressionRatioSoftSensor : ISensorDevice {
        public CompressionRatioSoftSensor(string sensorName, string procedureName) {
            SensorName = sensorName;
            ProcedureName = procedureName;
        }

        public string SensorName { get; private set; }

        public string ProcedureName { get; private set; }

        public object ObservePropertyValue(params object[] inputProperties) {
            // TODO: use size-calculating logic like for Custom-Piece.
            return 1;
        }
    }
}
