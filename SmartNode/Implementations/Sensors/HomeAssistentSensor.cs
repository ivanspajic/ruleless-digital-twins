using System.Diagnostics;
using System.Net.Http.Json;
using Logic.DeviceInterfaces;

namespace Implementations.Sensors {
    
    public class HomeAssistantSensor : ISensorDevice {
        private readonly HttpClient _httpClient;
        private readonly string? _attribute;

        public HomeAssistantSensor(string sensorName, string procedureName, string? attribute, HttpClient httpClient) {
            Debug.Assert(httpClient.BaseAddress != null, "HttpClient BaseAddress is not set.");
            SensorName = sensorName;
            ProcedureName = procedureName; // Currently used for sensor_id.
            _attribute = attribute; // Do we need to peek into the JSON structure beyond the `state`?
            _httpClient = httpClient;
        }
        
        public string SensorName { get; private set; }

        public string ProcedureName { get; private set; }

        record class SensorValue(double State); // For deserializing the JSON response

        public object ObservePropertyValue(params object[] inputProperties) {
            // Console.WriteLine($"Observing Home Assistant Sensor Value: {inputProperties[0]}");
            // We sometimes don't need the primary `state`, but need to look into the attributes,
            // 
            var requestUri = $"api/states/{ProcedureName}";
            // TODO: return value only works for double values at the moment.
            // TODO: streamline
            if (_attribute == null) {
                // Only sensor id given? Straightforward.
                var task = Task.Run(async () => await _httpClient.GetFromJsonAsync<SensorValue>(requestUri));
                var response = task.Result;
                Debug.Assert(response != null, "Response from Home Assistant is null.");
                return response.State;
            } else {
                // Do we need to peek into the JSON structure?
                var task = Task.Run(async () => await _httpClient.GetStringAsync(requestUri));
                var response = task.Result;
                Debug.Assert(response != null, "Response from Home Assistant is null.");
                var jsonDoc = System.Text.Json.JsonDocument.Parse(response);
                var value = jsonDoc.RootElement.GetProperty("attributes").GetProperty(_attribute);
                return value;
            }
        }
    }
}