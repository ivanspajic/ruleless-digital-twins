using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Logic.TTComponentInterfaces;

namespace Implementations.Sensors.HomeAssistant {
    
    public class HomeAssistantSensor : ISensor {
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

        record class HAAttributes(string? unit_of_measurement);
        record class SensorValue(double State, HAAttributes Attributes); // For deserializing the JSON response

        public async Task<object> ObservePropertyValue(params object[] inputProperties) {
            // Console.WriteLine($"Observing Home Assistant Sensor Value: {inputProperties[0]}");
            // We sometimes don't need the primary `state`, but need to look into the attributes,
            // 
            var requestUri = $"api/states/{ProcedureName}";
            // TODO: return value only works for double values at the moment.
            // TODO: streamline. Maybe we just go through the string anyways and pick up the necessary bits.
            if (_attribute == null) {
                // Only sensor id given? Straightforward.
                var response = _httpClient.GetFromJsonAsync<SensorValue>(requestUri).Result;
                Debug.Assert(response != null, "Response from Home Assistant is null.");
                // For units, we'd have to look into the attributes and look for "unit_of_measurement".                
                // Trace.WriteLine("unit: "+response.Attributes.unit_of_measurement);
                return response.State;
            } else {
                // Do we need to peek into the JSON structure?
                var response = _httpClient.GetStringAsync(requestUri).Result;
                Debug.Assert(response != null, "Response from Home Assistant is null.");
                // Apparently C# is not good at garbage collection? If we switch to .NET 10, apply CA2026 here
                using var jsonDoc = System.Text.Json.JsonDocument.Parse(response);
                // TODO: set to 0 if missing? Yr doesn't include precipitation values if it's dry, but the unit!
                jsonDoc.RootElement.GetProperty("attributes").TryGetProperty(_attribute, out var value);
                // TODO: Maybe we can eventually do something useful with it:
                jsonDoc.RootElement.GetProperty("attributes").TryGetProperty(_attribute + "_unit", out var unit);
                // We're disposing the JSON, so get the goods now:
                return value.ValueKind == JsonValueKind.Undefined ? 0.0 : value.GetDouble();
            }
        }
    }
}