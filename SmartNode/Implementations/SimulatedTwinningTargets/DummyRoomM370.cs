namespace Implementations.SimulatedTwinningTargets
{
    public class DummyRoomM370 {
        private readonly Random _randomGenerator = new();

        private double _roomTemperature = 17.7;
        private double _roomHumidity = 10.2;
        private double _energyConsumption = 0.0;

        private static DummyRoomM370? _instance;

        private DummyRoomM370() { }

        public static DummyRoomM370 Instance {
            get {
                _instance ??= new DummyRoomM370();

                return _instance;
            }
        }

        public double RoomTemperature
        {
            get => _roomTemperature;
            set
            {
                // Activate both mechanisms in case only one Actuator is a part of the simulation configurations.
                AffectTemperatureWithWeather(value);
                AffectHumidityWithWeather(_roomHumidity);
            }
        }

        public double RoomHumidity
        {
            get => _roomHumidity;
            set
            {
                AffectHumidityWithWeather(value);
                AffectTemperatureWithWeather(_roomTemperature);
            }
        }

        public double EnergyConsumption
        {
            get => _energyConsumption;
            set => _energyConsumption = value;
        }

        private void AffectTemperatureWithWeather(double temperature)
        {
            _roomTemperature = Math.Max(Math.Min(temperature - 6 + (_randomGenerator.NextDouble() * _randomGenerator.NextInt64(1, 5)), 30), 10);
        }

        private void AffectHumidityWithWeather(double humidity)
        {
            _roomHumidity = humidity + 2 + (_randomGenerator.NextDouble() * _randomGenerator.NextInt64(1, 2));
        }
    }
}
