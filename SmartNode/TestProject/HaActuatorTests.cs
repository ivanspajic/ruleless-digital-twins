using System.Net;
using Implementations.Actuators.HomeAssistant;

namespace TestProject;

/// <summary>
/// Offline tests for HaActuator: no live Home Assistant, no token. A recording
/// HttpMessageHandler captures the outgoing request so we can assert the RDT
/// state is translated to the correct HA service + payload, and that the actuator
/// only records a new state once Home Assistant accepted the call.
/// </summary>
public class HaActuatorTests {

    private sealed class RecordingHandler : HttpMessageHandler {
        private readonly HttpStatusCode _status;
        public string? RequestUri { get; private set; }
        public string? RequestBody { get; private set; }

        public RecordingHandler(HttpStatusCode status) => _status = status;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) {
            RequestUri = request.RequestUri?.ToString();
            RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(_status);
        }
    }

    private static (HaActuator actuator, RecordingHandler handler) Make(
            string entityId, IReadOnlyList<string> states, HttpStatusCode status = HttpStatusCode.OK) {
        var handler = new RecordingHandler(status);
        var http = new HttpClient(handler) { BaseAddress = new Uri("http://localhost:8123/") };
        var actuator = new HaActuator(entityId, entityId, states, http);
        return (actuator, handler);
    }

    [Fact]
    public async Task On_maps_to_turn_on() {
        var (actuator, handler) = Make("light.kitchen", new[] { "on", "off" });
        await actuator.Actuate("on");
        Assert.EndsWith("api/services/light/turn_on", handler.RequestUri);
        Assert.Contains("light.kitchen", handler.RequestBody);
    }

    [Fact]
    public async Task Off_maps_to_turn_off() {
        var (actuator, handler) = Make("switch.lab", new[] { "on", "off" });
        await actuator.Actuate("off");
        Assert.EndsWith("api/services/switch/turn_off", handler.RequestUri);
    }

    [Fact]
    public async Task Pct_maps_to_set_percentage_with_payload() {
        var (actuator, handler) = Make("fan.bedroom", new[] { "pct:0", "pct:50" });
        await actuator.Actuate("pct:50");
        Assert.EndsWith("api/services/fan/set_percentage", handler.RequestUri);
        Assert.Contains("\"percentage\":50", handler.RequestBody);
    }

    [Fact]
    public async Task Preset_maps_to_set_preset_mode_with_payload() {
        var (actuator, handler) = Make("climate.office", new[] { "preset:away", "preset:home" });
        await actuator.Actuate("preset:away");
        Assert.EndsWith("api/services/climate/set_preset_mode", handler.RequestUri);
        Assert.Contains("\"preset_mode\":\"away\"", handler.RequestBody);
    }

    [Fact]
    public async Task State_not_updated_when_http_call_fails() {
        var (actuator, _) = Make("light.kitchen", new[] { "on", "off" }, HttpStatusCode.InternalServerError);
        object before = actuator.ActuatorState; // initial = "on"
        await Assert.ThrowsAsync<HttpRequestException>(() => actuator.Actuate("off"));
        Assert.Equal(before, actuator.ActuatorState); // unchanged after failure
    }
}
