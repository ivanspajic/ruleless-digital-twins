using System.Globalization;
using System.Text;
using System.Text.Json;
using Logic.TTComponentInterfaces;

namespace Implementations.Actuators.HomeAssistant
{
    public class HomeAssistantActuator : IActuator
    {
        public enum ActuatorKind { InputBoolean, InputSelect, Light, Switch, InputNumber }

        private readonly HttpClient _httpClient;
        private readonly string _entityId;
        private readonly ActuatorKind _kind;
        private readonly string? _onOption;
        private int _state;

        public HomeAssistantActuator(string actuatorName, string entityId, ActuatorKind kind, HttpClient httpClient, string? onOption = null)
        {
            ActuatorName = actuatorName;
            _entityId = entityId;
            _kind = kind;
            _httpClient = httpClient;
            _onOption = onOption;
        }

        public string ActuatorName { get; }

        public object ActuatorState => _state;

        public async Task Actuate(object state)
        {
            double value = state switch
            {
                double d => d,
                int i => i,
                _ => double.Parse(state.ToString()!, CultureInfo.InvariantCulture)
            };

            if (_kind == ActuatorKind.InputNumber)
            {
                _state = (int)value;
                var body = JsonSerializer.Serialize(new { entity_id = _entityId, value });
                await Post("api/services/input_number/set_value", body);
                return;
            }

            int on = (int)value;
            _state = on;

            string url;
            string payload;

            switch (_kind)
            {
                case ActuatorKind.InputBoolean:
                    url = on == 1 ? "api/services/input_boolean/turn_on" : "api/services/input_boolean/turn_off";
                    payload = JsonSerializer.Serialize(new { entity_id = _entityId });
                    break;
                case ActuatorKind.Light:
                    url = on == 1 ? "api/services/light/turn_on" : "api/services/light/turn_off";
                    payload = JsonSerializer.Serialize(new { entity_id = _entityId });
                    break;
                case ActuatorKind.Switch:
                    url = on == 1 ? "api/services/switch/turn_on" : "api/services/switch/turn_off";
                    payload = JsonSerializer.Serialize(new { entity_id = _entityId });
                    break;
                default:
                    var option = on == 1 ? (_onOption ?? "on") : "off";
                    url = "api/services/input_select/select_option";
                    payload = JsonSerializer.Serialize(new { entity_id = _entityId, option });
                    break;
            }

            await Post(url, payload);
        }

        public void RunDummyEnvironment(double mapekExecutionDurationSeconds) { }

        private async Task Post(string url, string body)
        {
            var resp = await _httpClient.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));
            if (!resp.IsSuccessStatusCode)
            {
                System.Diagnostics.Trace.WriteLine($"HA actuate {_entityId} ({_kind}) returned {(int)resp.StatusCode}");
            }
        }
    }
}
