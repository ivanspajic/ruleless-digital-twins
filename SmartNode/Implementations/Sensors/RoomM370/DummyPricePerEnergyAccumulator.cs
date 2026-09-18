using Implementations.SimulatedTwinningTargets;
using Logic.TTComponentInterfaces;
using System.Globalization;

namespace Implementations.Sensors.RoomM370 {
    public class DummyPricePerEnergyAccumulatorSensor : ISensor {
        private readonly DummyRoomM370 _dummyRoomM370;

        public DummyPricePerEnergyAccumulatorSensor(string sensorName, string procedureName, DummyRoomM370 dummyRoomM370) {
            SensorName = sensorName;
            ProcedureName = procedureName;
            _dummyRoomM370 = dummyRoomM370;
        }

        public string SensorName { get; private set; }

        public string ProcedureName { get; private set; }

        public async Task<object> ObservePropertyValue(params object[] inputProperties) {
            if (inputProperties[0] is not double) {
                inputProperties[0] = double.Parse(inputProperties[0].ToString()!, CultureInfo.InvariantCulture);
            }

            _dummyRoomM370.PricePerEnergyAccumulated += (double)inputProperties[0];

            return _dummyRoomM370.PricePerEnergyAccumulated;
        }
    }
}
