namespace Implementations.SimulatedTwinningTargets
{
    public class DummyRoomM370
    {
        private readonly Random _randomGenerator = new();

        private double _roomTemperature = 10.0;
        private double _roomHumidity = 10.0;
        private double _energyConsumption = 0.0;

        public double RoomTemperature
        {
            get => _roomTemperature;
            set => AffectTemperatureWithWeather(value);
        }

        public double RoomHumidity
        {
            get => _roomHumidity;
            set => AffectHumidityWithWeather(value);
        }

        public double EnergyConsumption
        {
            get => _energyConsumption;
            set => _energyConsumption = value;
        }

        private void AffectTemperatureWithWeather(double temperature)
        {
            _roomTemperature = temperature - (_randomGenerator.NextDouble() * _randomGenerator.NextInt64(1, 5));
        }

        private void AffectHumidityWithWeather(double humidity)
        {
            _roomHumidity = humidity + (_randomGenerator.NextDouble() * _randomGenerator.NextInt64(1, 2));
        }
    }
}
