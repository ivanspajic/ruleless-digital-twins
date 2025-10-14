namespace Implementations.SimulatedTwinningTargets
{
    public class DummyRoomM370
    {
        private double _roomTemperature = 10.0;
        private double _roomHumidity = 10.0;
        private double _energyConsumption = 0.0;

        public double RoomTemperature
        {
            get => _roomTemperature;
            set => _roomTemperature = value;
        }

        public double RoomHumidity
        {
            get => _roomHumidity;
            set => _roomHumidity = value;
        }

        public double EnergyConsumption
        {
            get => _energyConsumption;
            set => _energyConsumption = value;
        }
    }
}
