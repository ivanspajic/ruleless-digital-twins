namespace Implementations.Actuators.HomeAssistant {

    /// <summary>
    /// Builds the <see cref="HttpClient"/> used to talk to a Home Assistant
    /// instance. Kept in Implementations so that the generic core never holds
    /// Home Assistant transport details (base URL, bearer token).
    ///
    /// Environment variables:
    ///   HA_BASE_URL (or HA_URL)  — host root, e.g. "http://homeassistant.local:8123/"
    ///   HA_TOKEN    (or TOKEN_HA) — long-lived access token
    /// </summary>
    public static class HomeAssistantConnection {

        public static HttpClient CreateFromEnvironment() {
            string baseUrl = Environment.GetEnvironmentVariable("HA_BASE_URL")
                ?? Environment.GetEnvironmentVariable("HA_URL")
                ?? throw new InvalidOperationException(
                    "HA_BASE_URL (or HA_URL) environment variable is required to reach Home Assistant.");
            string token = Environment.GetEnvironmentVariable("HA_TOKEN")
                ?? Environment.GetEnvironmentVariable("TOKEN_HA")
                ?? throw new InvalidOperationException(
                    "HA_TOKEN (or TOKEN_HA) environment variable is required to reach Home Assistant.");

            if (!baseUrl.EndsWith('/')) baseUrl += "/";

            var http = new HttpClient(new SocketsHttpHandler {
                PooledConnectionLifetime = TimeSpan.FromMinutes(2)
            }) {
                BaseAddress = new Uri(baseUrl)
            };
            http.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            http.DefaultRequestHeaders.Add("User-Agent", "SmartNode-HA/1.0");
            return http;
        }
    }
}
