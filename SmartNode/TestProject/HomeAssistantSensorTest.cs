using Microsoft.Extensions.Configuration;
using Implementations.Sensors;

namespace TestProject;

public class HomeAssistantSensorTest {

    [Theory]
    [InlineData("MH30", "https://mh30.foldr.org:8123/", "HA:TOKEN", "sensor.pwr", null)]
    [InlineData("IoTLab", "http://100.104.156.81:8123/", "HA:TOKEN_IOTLAB", "weather.forecast_home", "temperature")]
    public void TestObservePropertyValue(string id, string url, string tokenName, string sensorId, string? attribute) {
        var secrets = new ConfigurationBuilder()
            .AddUserSecrets<HomeAssistantSensorTest>()
            .Build();
        var TOKEN = secrets[tokenName];
        Assert.SkipWhen(TOKEN == null, $"No token for host {id}.");

        var httpClient = new HttpClient {
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
