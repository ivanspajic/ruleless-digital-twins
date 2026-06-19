using Implementations.Actuators.HomeAssistant;
using Implementations.Sensors.HomeAssistant;
using Logic.FactoryInterface;
using Logic.TTComponentInterfaces;
using SmartNode.Services.Bindings;

namespace SmartNode.Factories {

    /// <summary>
    /// Factory populated entirely from a TTL SOSA/RDT bindings file, selected when
    /// CoordinatorSettings.Environment == "homeassistant".
    ///
    /// Environment variables:
    ///   HA_BINDINGS_TTL          — path to the SOSA/RDT instance produced by hass-to-rdt
    ///   HA_BASE_URL / HA_TOKEN   — Home Assistant connection (see HomeAssistantConnection)
    ///
    /// Home Assistant transport (HttpClient, bearer token) lives in Implementations
    /// via <see cref="HomeAssistantConnection"/>, so the generic core stays HA-agnostic.
    /// </summary>
    public class HomeAssistantFactory : AbstractFactory, IFactory {

        public HomeAssistantFactory(IServiceProvider serviceProvider)
            : base(BuildContext(serviceProvider)) { }

        // Internal ctor used by tests to inject explicit bindings + HttpClient.
        internal HomeAssistantFactory(BindingsResult bindings, HttpClient httpClient, IServiceProvider serviceProvider)
            : base(new BindingsServiceProvider(bindings, httpClient, serviceProvider)) { }

        private static IServiceProvider BuildContext(IServiceProvider inner) {
            string ttlPath = Environment.GetEnvironmentVariable("HA_BINDINGS_TTL")
                ?? throw new InvalidOperationException(
                    "HA_BINDINGS_TTL environment variable is required for the homeassistant environment.");
            BindingsResult bindings = TtlBindingsLoader.Load(ttlPath)
                ?? throw new InvalidOperationException(
                    $"Could not load TTL bindings from '{ttlPath}'.");
            HttpClient http = HomeAssistantConnection.CreateFromEnvironment();
            return new BindingsServiceProvider(bindings, http, inner);
        }

        protected override IDictionary<(string, string), ISensor> MakeSensorMap(IServiceProvider serviceProvider) {
            var ctx      = (BindingsServiceProvider)serviceProvider;
            var bindings = ctx.Bindings;
            var http     = ctx.HttpClient;

            var map = new Dictionary<(string, string), ISensor>();
            foreach (var (_, sb) in bindings.SensorMap) {
                if (string.IsNullOrEmpty(sb.HaEntityId)) {
                    Console.WriteLine($"[HomeAssistantFactory] Skipping sensor '{sb.Uri}' — no HA entity id (rdt:hasIdentifier).");
                    continue;
                }
                string procedureKey = string.IsNullOrEmpty(sb.ProcedureUri) ? sb.Uri : sb.ProcedureUri;
                map[(sb.Uri, procedureKey)] =
                    new HomeAssistantSensor(sb.Uri, sb.HaEntityId, attribute: null, http);
            }
            Console.WriteLine($"[HomeAssistantFactory] Built {map.Count} sensor(s) from TTL bindings.");
            return map;
        }

        protected override IDictionary<string, IActuator> MakeActuatorMap(IServiceProvider serviceProvider) {
            var ctx      = (BindingsServiceProvider)serviceProvider;
            var bindings = ctx.Bindings;
            var http     = ctx.HttpClient;

            var map = new Dictionary<string, IActuator>();
            foreach (var (_, ab) in bindings.ActuatorMap) {
                if (string.IsNullOrEmpty(ab.HaEntityId)) {
                    Console.WriteLine($"[HomeAssistantFactory] Skipping actuator '{ab.Uri}' — no HA entity id (rdt:hasIdentifier).");
                    continue;
                }
                map[ab.Uri] = new HaActuator(ab.Uri, ab.HaEntityId, ab.PossibleStates, http);
            }
            Console.WriteLine($"[HomeAssistantFactory] Built {map.Count} actuator(s) from TTL bindings.");
            return map;
        }

        protected override IDictionary<string, IConfigurableParameter> MakeConfigurableParameterMap(IServiceProvider serviceProvider) {
            // ConfigurableParameters (continuous levers) are intentionally left out of
            // this factory for now; they need a separate, carefully reviewed PR.
            return new Dictionary<string, IConfigurableParameter>();
        }

        /// <summary>
        /// Thin wrapper carrying the loaded bindings and HttpClient through
        /// AbstractFactory's IServiceProvider parameter (the base ctor builds the
        /// maps before the subclass body runs), without changing the base contract.
        /// </summary>
        private sealed class BindingsServiceProvider : IServiceProvider {
            private readonly IServiceProvider _inner;

            public BindingsServiceProvider(BindingsResult bindings, HttpClient httpClient, IServiceProvider inner) {
                Bindings   = bindings;
                HttpClient = httpClient;
                _inner     = inner;
            }

            public BindingsResult Bindings   { get; }
            public HttpClient     HttpClient { get; }

            public object? GetService(Type serviceType) => _inner.GetService(serviceType);
        }
    }
}
