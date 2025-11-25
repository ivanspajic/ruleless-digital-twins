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

        record class HAAttributes(string? unit_of_measurement);
        record class SensorValue(double State, HAAttributes Attributes); // For deserializing the JSON response

        public object ObservePropertyValue(params object[] inputProperties) {
            // Console.WriteLine($"Observing Home Assistant Sensor Value: {inputProperties[0]}");
            // We sometimes don't need the primary `state`, but need to look into the attributes,
            // 
            var requestUri = $"api/states/{ProcedureName}";
            // TODO: return value only works for double values at the moment.
            // TODO: streamline. Maybe we just go through the string anyways and pick up the necessary bits.
            if (_attribute == null) {
                // Only sensor id given? Straightforward.
                var response = _httpClient.GetFromJsonAsync<SensorValue>(requestUri).GetAwaiter().GetResult();
                Debug.Assert(response != null, "Response from Home Assistant is null.");
                // For units, we'd have to look into the attributes and look for "unit_of_measurement".                
                // Trace.WriteLine("unit: "+response.Attributes.unit_of_measurement);
                return response.State;
            } else {
                // Do we need to peek into the JSON structure?
                var task = Task.Run(async () => await _httpClient.GetStringAsync(requestUri));
                var response = task.Result;
                Debug.Assert(response != null, "Response from Home Assistant is null.");
                var bytes = System.Text.Encoding.UTF8.GetBytes(response);
                var reader = new System.Text.Json.Utf8JsonReader(bytes);
                var jsonElement = System.Text.Json.JsonElement.ParseValue(ref reader);
                // TODO: set to 0 if missing? Yr doesn't include precipitation values if it's dry, but the unit!
                jsonElement.GetProperty("attributes").TryGetProperty(_attribute, out var value);
                // TODO: Maybe we can eventually do something useful with it:
                jsonElement.GetProperty("attributes").TryGetProperty(_attribute + "_unit", out var unit);
                return value;
            }
        }
    }
}