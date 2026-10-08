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
                var _ when SensorName.EndsWith("fmu_ceiHeiSensor") => 1.0,
                var _ when SensorName.EndsWith("fmu_cloTimSensor") => 0.0,
                var _ when SensorName.EndsWith("fmu_HDirNorSensor") => 0.0,
                var _ when SensorName.EndsWith("fmu_HGloHorSensor") => 0.0,
                var _ when SensorName.EndsWith("fmu_HHorIRSensor") => 0.0,
                var _ when SensorName.EndsWith("fmu_latSensor") => 30.6,
                var _ when SensorName.EndsWith("fmu_lonSensor") => 24.5,
                var _ when SensorName.EndsWith("fmu_nOpaSensor") => 1.0,
                var _ when SensorName.EndsWith("fmu_nTotSensor") => 1.0,
                var _ when SensorName.EndsWith("fmu_pAtmSensor") => 98900.0,
                var _ when SensorName.EndsWith("fmu_relHumSensor") => 1.0,
                var _ when SensorName.EndsWith("fmu_solAltSensor") => 1.0,
                var _ when SensorName.EndsWith("fmu_solDecSensor") => 1.0,
                var _ when SensorName.EndsWith("fmu_solHouAngSensor") => 1.0,
                var _ when SensorName.EndsWith("fmu_solTimSensor") => 36000.0,
                var _ when SensorName.EndsWith("fmu_solZenSensor") => 60.0,
                var _ when SensorName.EndsWith("fmu_TBlaSkySensor") => 295.0,
                var _ when SensorName.EndsWith("fmu_TDewPoiSensor") => 288.15,
                var _ when SensorName.EndsWith("fmu_TDryBulSensor") => 305.0,
                var _ when SensorName.EndsWith("fmu_TWetBulSensor") => 293.15,
                var _ when SensorName.EndsWith("fmu_winDirSensor") => 0.0,
                var _ when SensorName.EndsWith("fmu_winSpeSensor") => 1.0,
                _ => 0.0,
            };
        }
    }
}
