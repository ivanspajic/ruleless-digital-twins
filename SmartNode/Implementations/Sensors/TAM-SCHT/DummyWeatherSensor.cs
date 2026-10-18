using Logic.TTComponentInterfaces;

namespace Implementations.Sensors.TAM_SCHT {
    public class DummyWeatherSensor : ISensor {
        public DummyWeatherSensor(string sensorName, string procedureName) {
            SensorName = sensorName;
            ProcedureName = procedureName;
        }

        public string SensorName { get; private set; }

        public string ProcedureName { get; private set; }

        public async Task<object> ObservePropertyValue(params object[] inputProperties) {
            return SensorName switch {
                var _ when SensorName.EndsWith("fmu_altSensor") => 90.0,
                var _ when SensorName.EndsWith("fmu_ceiHeiSensor") => 30.0,
                var _ when SensorName.EndsWith("fmu_cloTimSensor") => 18704100.0,
                var _ when SensorName.EndsWith("fmu_HDifHorSensor") => 0.0,
                var _ when SensorName.EndsWith("fmu_HDirNorSensor") => 0.0,
                var _ when SensorName.EndsWith("fmu_HGloHorSensor") => 0.0,
                var _ when SensorName.EndsWith("fmu_HHorIRSensor") => 0.0,
                var _ when SensorName.EndsWith("fmu_latSensor") => 0.534,
                var _ when SensorName.EndsWith("fmu_lonSensor") => 0.428,
                var _ when SensorName.EndsWith("fmu_nOpaSensor") => 1.0,
                var _ when SensorName.EndsWith("fmu_nTotSensor") => 1.0,
                var _ when SensorName.EndsWith("fmu_pAtmSensor") => 101450.0,
                var _ when SensorName.EndsWith("fmu_relHumSensor") => 0.528,
                var _ when SensorName.EndsWith("fmu_solAltSensor") => 1.0,
                var _ when SensorName.EndsWith("fmu_solDecSensor") => 1.0,
                var _ when SensorName.EndsWith("fmu_solHouAngSensor") => 1.0,
                var _ when SensorName.EndsWith("fmu_solTimSensor") => 36000.0,
                var _ when SensorName.EndsWith("fmu_solZenSensor") => 1.047,
                var _ when SensorName.EndsWith("fmu_TBlaSkySensor") => 295.0,
                var _ when SensorName.EndsWith("fmu_TDewPoiSensor") => 295.3,
                var _ when SensorName.EndsWith("fmu_TDryBulSensor") => 305.8,
                var _ when SensorName.EndsWith("fmu_TWetBulSensor") => 298.15,
                var _ when SensorName.EndsWith("fmu_winDirSensor") => 3.68,
                var _ when SensorName.EndsWith("fmu_winSpeSensor") => 4.0,
                var _ when SensorName.EndsWith("HouseTemp") => 24.3,
                _ => 0.0,
            };
        }
    }
}
