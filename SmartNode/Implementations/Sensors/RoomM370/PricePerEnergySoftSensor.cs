using Logic.TTComponentInterfaces;
using System.Globalization;

namespace Implementations.Sensors.RoomM370 {
    public class PricePerEnergySoftSensor : ISensor {
        public PricePerEnergySoftSensor(string sensorName, string procedureName, Func<double, double, double> func) {
            SensorName = sensorName;
            ProcedureName = procedureName;
            Func = func;
        }

        public string SensorName { get; }

        public string ProcedureName { get; }
        public Func<double, double, double> Func { get; }

        public async Task<object> ObservePropertyValue(params object[] inputProperties) {
            for (var i = 0; i < inputProperties.Length; i++) {
                if (inputProperties[i] is not double) {
                    inputProperties[i] = double.Parse(inputProperties[i].ToString()!, CultureInfo.InvariantCulture);
                }
            }

            return Func((double)inputProperties[0], (double)inputProperties[1]); // XXX Review
        }
    }
}
