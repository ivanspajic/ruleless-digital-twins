using SmartNode.Services.Bindings;
using Xunit;

namespace TestProject
{
    /// <summary>
    /// Bindings-layer verification for TtlBindingsLoader. Hermetic: writes small
    /// in-memory TTL fixtures to a temp file and parses them — no FMU / Python /
    /// Java and no external model files needed.
    ///
    /// Proves the loader resolves both TTL dialects used in this repo:
    ///   - subclass typing:  :x a hass:Light, where hass:Light rdfs:subClassOf sosa:Actuator
    ///   - direct typing:     :y a sosa:Actuator
    /// </summary>
    public class TtlBindingsLoaderTests
    {
        private const string Ttl = """
            @prefix sosa: <http://www.w3.org/ns/sosa/> .
            @prefix ssn:  <http://www.w3.org/ns/ssn/> .
            @prefix rdfs: <http://www.w3.org/2000/01/rdf-schema#> .
            @prefix rdt:  <http://www.semanticweb.org/ivans/ontologies/2025/ruleless-digital-twins/> .
            @prefix ex:   <http://example.org/ha/> .
            @prefix hass: <http://example.org/hass/> .

            hass:Light rdfs:subClassOf sosa:Actuator .

            ex:KitchenLight a hass:Light ;
                rdt:hasIdentifier "light.kitchen" ;
                rdt:hasActuatorState "on" ;
                rdt:hasActuatorState "off" .

            ex:LabFan a sosa:Actuator ;
                rdt:hasIdentifier "fan.lab" ;
                rdt:hasActuatorState "on" ;
                rdt:hasActuatorState "off" .

            ex:TempSensor a sosa:Sensor ;
                rdt:hasIdentifier "sensor.temp" ;
                sosa:observes ex:Temperature ;
                ssn:implements ex:TempProcedure .
            """;

        private static BindingsResult LoadFixture()
        {
            string path = Path.Combine(Path.GetTempPath(), $"rdt-bindings-{Guid.NewGuid():N}.ttl");
            File.WriteAllText(path, Ttl);
            try
            {
                var result = TtlBindingsLoader.Load(path);
                Assert.NotNull(result);
                return result!;
            }
            finally
            {
                File.Delete(path);
            }
        }

        [Fact]
        public void Resolves_subclass_typed_actuator()
        {
            var result = LoadFixture();

            // 2 actuators: KitchenLight (subclass of sosa:Actuator) + LabFan (direct).
            // The subclass one is the regression target (was missed before the fix).
            Assert.Equal(2, result.ActuatorMap.Count);
            Assert.Contains(result.ActuatorMap.Values, a => a.HaEntityId == "light.kitchen");
        }

        [Fact]
        public void Resolves_direct_typed_actuator_and_states()
        {
            var result = LoadFixture();

            Assert.Contains(result.ActuatorMap.Values, a => a.HaEntityId == "fan.lab");
            // Every actuator carries an entity id and at least one state.
            Assert.All(result.ActuatorMap.Values, a => Assert.False(string.IsNullOrEmpty(a.HaEntityId)));
            Assert.All(result.ActuatorMap.Values, a => Assert.NotEmpty(a.PossibleStates));
        }

        [Fact]
        public void Resolves_sensor_with_identifier()
        {
            var result = LoadFixture();

            Assert.Single(result.SensorMap);
            Assert.Contains(result.SensorMap.Values, s => s.HaEntityId == "sensor.temp");
        }
    }
}
