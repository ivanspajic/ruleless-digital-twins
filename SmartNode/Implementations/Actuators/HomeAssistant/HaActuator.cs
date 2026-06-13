using Logic.TTComponentInterfaces;
using System.Net.Http.Json;

namespace Implementations.Actuators.HomeAssistant {

    /// <summary>
    /// Sends service calls to Home Assistant to actuate a device.
    /// The HA entity domain is derived from the entity id (e.g. "switch.lab" → "switch").
    /// RDT states are mapped to real HA services by <see cref="HaActionTranslator"/>.
    /// </summary>
    public class HaActuator : IActuator {
        private readonly HttpClient _httpClient;
        private readonly IReadOnlyList<string> _possibleStates;
        private object _currentState;

        public HaActuator(string actuatorName, string haEntityId, IReadOnlyList<string> possibleStates, HttpClient httpClient) {
            ActuatorName    = actuatorName;
            HaEntityId      = haEntityId;
            _possibleStates = possibleStates;
            _httpClient     = httpClient;
            _currentState   = possibleStates.Count > 0 ? possibleStates[0] : "unknown";
        }

        public string ActuatorName { get; }

        public string HaEntityId { get; }

        public object ActuatorState => _currentState;

        public async Task Actuate(object state) {
            string stateStr = state?.ToString() ?? string.Empty;

            // Derive domain from entity id, e.g. "switch.lab_light" → "switch".
            int dotIndex = HaEntityId.IndexOf('.');
            string domain = dotIndex >= 0 ? HaEntityId[..dotIndex] : HaEntityId;

            // Map the RDT state to a real HA service + payload.
            var call = HaActionTranslator.Translate(domain, HaEntityId, stateStr);
            var requestUri = $"api/services/{domain}/{call.Service}";

            var response = await _httpClient.PostAsJsonAsync(requestUri, call.Data);
            response.EnsureSuccessStatusCode();

            // Only record the new state once Home Assistant accepted the call.
            _currentState = stateStr;
        }
    }
}
