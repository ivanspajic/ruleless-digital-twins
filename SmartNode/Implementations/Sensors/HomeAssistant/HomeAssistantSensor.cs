using System.Diagnostics;
using System.Globalization;
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

        public async Task<object> ObservePropertyValue(params object[] inputProperties) {
            var requestUri = $"api/states/{ProcedureName}";
            // If HA is unreachable or returns malformed data, return a neutral 0.0 so the MAPE-K loop
            // can continue with a degraded reading instead of bubbling an exception that kills it.
            try {
                var response = await _httpClient.GetStringAsync(requestUri);
                if (string.IsNullOrWhiteSpace(response)) {
                    Trace.WriteLine($"HA sensor {ProcedureName} ({requestUri}) returned empty payload - falling back to 0.0");
                    return 0.0;
                }

                using var jsonDoc = JsonDocument.Parse(response);
                var root = jsonDoc.RootElement;

                if (_attribute == null) {
                    if (root.TryGetProperty("state", out var state) && TryReadDouble(state, out var stateValue)) {
                        return stateValue;
                    }

                    Trace.WriteLine($"HA sensor {ProcedureName} ({requestUri}) state is missing or non-numeric ({DescribeJsonValue(state)}) - falling back to 0.0");
                    return 0.0;
                }

                if (!root.TryGetProperty("attributes", out var attributes)) {
                    Trace.WriteLine($"HA sensor {ProcedureName} ({requestUri}) has no attributes object - falling back to 0.0");
                    return 0.0;
                }

                if (attributes.TryGetProperty(_attribute, out var attributeValue) && TryReadDouble(attributeValue, out var parsedAttribute)) {
                    return parsedAttribute;
                }

                Trace.WriteLine($"HA sensor {ProcedureName} ({requestUri}) attribute '{_attribute}' is missing or non-numeric ({DescribeJsonValue(attributeValue)}) - falling back to 0.0");
                return 0.0;
            } catch (Exception ex) {
                Trace.WriteLine($"HA sensor {ProcedureName} ({requestUri}) unreachable or malformed: {ex.Message} - falling back to 0.0");
                return 0.0;
            }
        }

        private static bool TryReadDouble(JsonElement element, out double value) {
            value = 0.0;
            switch (element.ValueKind) {
                case JsonValueKind.Number:
                    return element.TryGetDouble(out value);

                case JsonValueKind.String:
                    var text = element.GetString();
                    if (string.IsNullOrWhiteSpace(text)) {
                        return false;
                    }

                    var trimmed = text.Trim();
                    if (trimmed.Equals("unknown", StringComparison.OrdinalIgnoreCase) ||
                        trimmed.Equals("unavailable", StringComparison.OrdinalIgnoreCase)) {
                        return false;
                    }

                    return double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

                default:
                    return false;
            }
        }

        private static string DescribeJsonValue(JsonElement element) {
            return element.ValueKind switch {
                JsonValueKind.Undefined => "undefined",
                JsonValueKind.Null => "null",
                JsonValueKind.String => element.GetString() ?? "null",
                _ => element.ToString()
            };
        }
    }
}
