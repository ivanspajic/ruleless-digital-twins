using Implementations.SimulatedTwinningTargets;
using Logic.TTComponentInterfaces;

namespace Implementations.Sensors
{
    public class DummyEnergyConsumptionSensor : ISensorDevice
    {
        private readonly DummyRoomM370 _dummyRoomM370;

        public DummyEnergyConsumptionSensor(string sensorName, string procedureName, DummyRoomM370 dummyRoomM370)
        {
            SensorName = sensorName;
            ProcedureName = procedureName;
            _dummyRoomM370 = dummyRoomM370;
        }

        public string SensorName { get; private set; }

        public string ProcedureName { get; private set; }

        public object ObservePropertyValue(params object[] inputProperties)
        {
            return _dummyRoomM370.EnergyConsumption;
        }
    }
}
