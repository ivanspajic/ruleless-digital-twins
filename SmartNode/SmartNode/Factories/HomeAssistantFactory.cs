using System.Net.Http.Headers;
using Implementations.Actuators.HomeAssistant;
using Implementations.Sensors.HomeAssistant;
using Logic.FactoryInterface;
using Logic.TTComponentInterfaces;
using SmartNode.HaBindings;

namespace SmartNode.Factories
{
    public class HomeAssistantFactory : AbstractFactory, IFactory
    {
        private static HaBindingsConfig? _config;
        private static HttpClient? _httpClient;

        public HomeAssistantFactory(IServiceProvider serviceProvider) : this(Wrapper(serviceProvider)) { }

        private HomeAssistantFactory(Wrapped w) : base(w.ServiceProvider) { }

        private static Wrapped Wrapper(IServiceProvider serviceProvider)
        {
            EnsureLoaded();
            return new Wrapped(serviceProvider);
        }

        private readonly record struct Wrapped(IServiceProvider ServiceProvider);

        private static void EnsureLoaded()
        {
            if (_config != null && _httpClient != null) return;

            var path = Environment.GetEnvironmentVariable("HA_BINDINGS_FILE");
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new Exception("HA_BINDINGS_FILE environment variable is required for the homeassistant factory.");
            }

            var url = Environment.GetEnvironmentVariable("HA_URL");
            var token = Environment.GetEnvironmentVariable("HA_TOKEN");
            if (string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(token))
            {
                throw new Exception("HA_URL and HA_TOKEN environment variables are required for the homeassistant factory.");
            }

            _config = HaBindingsLoader.Load(path);

            _httpClient = new HttpClient { BaseAddress = new Uri(url.TrimEnd('/') + "/") };
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        protected override IDictionary<(string, string), ISensor> MakeSensorMap(IServiceProvider serviceProvider)
        {
            EnsureLoaded();
            var map = new Dictionary<(string, string), ISensor>();
            foreach (var s in _config!.Sensors)
            {
                map[(s.SensorUri, s.ProcedureUri)] = new HomeAssistantSensor(s.SensorUri, s.ProcedureUri, s.Attribute, _httpClient!);
            }
            return map;
        }

        protected override IDictionary<string, IActuator> MakeActuatorMap(IServiceProvider serviceProvider)
        {
            EnsureLoaded();
            var map = new Dictionary<string, IActuator>();
            foreach (var a in _config!.Actuators)
            {
                map[a.ActuatorUri] = new HomeAssistantActuator(a.ActuatorUri, a.HaEntityId, a.Kind, _httpClient!, a.OnOption);
            }
            return map;
        }

        protected override IDictionary<string, IConfigurableParameter> MakeConfigurableParameterMap(IServiceProvider serviceProvider)
        {
            return new Dictionary<string, IConfigurableParameter>();
        }
    }
}
