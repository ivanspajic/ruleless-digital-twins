using Logic.TTComponentInterfaces;
using System.Globalization;

namespace Implementations.Sensors.RoomM370 {
    public class IncreaseTemperatureSoftSensor : ISensor {
        public IncreaseTemperatureSoftSensor(string sensorName, string procedureName) {
            SensorName = sensorName;
            ProcedureName = procedureName;
        }

        public string SensorName { get; private set; }

        public string ProcedureName { get; private set; }

        public object ObservePropertyValue(params object[] inputProperties) {
            var input1 = inputProperties[0];
            var input2 = inputProperties[1];

            if (input1 is not double) {
                input1 = double.Parse(input1.ToString()!, CultureInfo.InvariantCulture);
            }
            if (input2 is not double) {
                input2 = double.Parse(input2.ToString()!, CultureInfo.InvariantCulture);
            }

            return ((double)input1 + (double)input2 + 5) / 2;
        }
    }
}
