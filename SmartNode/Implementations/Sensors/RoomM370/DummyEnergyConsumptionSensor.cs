using Implementations.SimulatedTwinningTargets;
using Logic.TTComponentInterfaces;

namespace Implementations.Sensors.RoomM370
{
    public class DummyEnergyConsumptionSensor : ISensor
    {
        private readonly DummyRoomM370 _dummyRoomM370;

        public DummyEnergyConsumptionSensor(string sensorName, string procedureName)
        {
            SensorName = sensorName;
            ProcedureName = procedureName;
            _dummyRoomM370 = DummyRoomM370.Instance;
        }

        public string SensorName { get; private set; }

        public string ProcedureName { get; private set; }

        public object ObservePropertyValue(params object[] inputProperties)
        {
            return _dummyRoomM370.EnergyConsumption;
        }
    }
}
