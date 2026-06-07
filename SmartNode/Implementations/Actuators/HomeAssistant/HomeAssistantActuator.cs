using Logic.TTComponentInterfaces;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Implementations.Actuators.HomeAssistant
{
    public class HomeAssistantActuator : IActuator
    {
        private readonly HttpClient _httpClient;
        private readonly string _entityId;
        private readonly ActuatorKind _kind;
        private readonly string? _onOption;
        private int _state = 0;

        public enum ActuatorKind { InputBoolean, InputSelect, Light, Switch, InputNumber }

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
            // Accept int or double — JSON from /api/actuate sends Number which we parse as double.
            double numericState = state switch {
                double d => d,
                int i    => i,
                _        => double.Parse(state.ToString()!, CultureInfo.InvariantCulture)
            };

            // InputNumber needs the raw numeric value; others are binary 0/1.
            if (_kind == ActuatorKind.InputNumber)
            {
                _state = (int)numericState;
                var requestUri = "api/services/input_number/set_value";
                var body = JsonSerializer.Serialize(new { entity_id = _entityId, value = numericState });
                var resp = await _httpClient.PostAsync(requestUri, new StringContent(body, Encoding.UTF8, "application/json"));
                if (!resp.IsSuccessStatusCode)
                {
                    System.Diagnostics.Trace.WriteLine($"HA actuate {_entityId} (input_number) returned {(int)resp.StatusCode}");
                }
                return;
            }

            int intState = (int)numericState;
            _state = intState;

            string requestUri2;
            string body2;

            if (_kind == ActuatorKind.InputBoolean)
            {
                var service = intState == 1 ? "input_boolean/turn_on" : "input_boolean/turn_off";
                requestUri2 = $"api/services/{service}";
                body2 = JsonSerializer.Serialize(new { entity_id = _entityId });
            }
            else if (_kind == ActuatorKind.Light)
            {
                var service = intState == 1 ? "light/turn_on" : "light/turn_off";
                requestUri2 = $"api/services/{service}";
                body2 = JsonSerializer.Serialize(new { entity_id = _entityId });
            }
            else if (_kind == ActuatorKind.Switch)
            {
                var service = intState == 1 ? "switch/turn_on" : "switch/turn_off";
                requestUri2 = $"api/services/{service}";
                body2 = JsonSerializer.Serialize(new { entity_id = _entityId });
            }
            else
            {
                var option = intState == 1 ? (_onOption ?? "on") : "off";
                requestUri2 = "api/services/input_select/select_option";
                body2 = JsonSerializer.Serialize(new { entity_id = _entityId, option });
            }

            var resp2 = await _httpClient.PostAsync(requestUri2, new StringContent(body2, Encoding.UTF8, "application/json"));
            if (!resp2.IsSuccessStatusCode)
            {
                System.Diagnostics.Trace.WriteLine($"HA actuate {_entityId} ({_kind}) returned {(int)resp2.StatusCode}");
            }
        }

        public void RunDummyEnvironment(double mapekExecutionDurationSeconds) { }
    }
}
