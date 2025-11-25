using Microsoft.Extensions.Configuration;
using Implementations.Sensors;

namespace TestProject;

public class HomeAssistantSensorTest : IDisposable {
    private readonly SocketsHttpHandler _handler;

    public HomeAssistantSensorTest() {
        _handler = new SocketsHttpHandler {
            PooledConnectionLifetime = TimeSpan.FromMinutes(2)
        };
    }

    public void Dispose() {
        _handler.Dispose();
    }

    [Theory]
    [InlineData("MH30", "https://mh30.foldr.org:8123/", "HA:TOKEN", "sensor.pwr", null)]
    [InlineData("IoTLab", "http://100.104.156.81:8123/", "HA:TOKEN_IOTLAB", "sensor.nordpool_kwh_no5_nok_3_10_025", null)]
    // example with attributes:
    [InlineData("IoTLab-temperature", "http://100.104.156.81:8123/", "HA:TOKEN_IOTLAB", "weather.forecast_home", "temperature")]
    // example with potentially missing data & units, schema at https://developers.home-assistant.io/docs/core/entity/weather/:
    [InlineData("IoTLab-precipitation", "http://100.104.156.81:8123/", "HA:TOKEN_IOTLAB", "weather.forecast_home", "precipitation")]
    public void TestObservePropertyValue(string id, string url, string tokenName, string sensorId, string? attribute) {
        var secrets = new ConfigurationBuilder()
            .AddUserSecrets<HomeAssistantSensorTest>()
            .Build();
        var TOKEN = secrets[tokenName];
        Assert.SkipWhen(TOKEN == null, $"No token for host {id}.");

        var httpClient = new HttpClient(_handler, disposeHandler: false) {
            BaseAddress = new Uri(url)
        };
        httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {TOKEN}");
        httpClient.DefaultRequestHeaders.Add("User-Agent", "SmartNodeTestClient/1.0");

        var sensor = new HomeAssistantSensor("TestSensor", sensorId, attribute, httpClient);
        var value = sensor.ObservePropertyValue();

        // Prints stuff in the Debug Console:
        System.Diagnostics.Trace.WriteLine($"Observed Sensor Value: {value}");
    }
}
