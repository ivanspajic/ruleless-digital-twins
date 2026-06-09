using System.Net;

namespace SmartNode.Services.HomeAssistant;

/// <summary>
/// Real <see cref="IHaCatalogReader"/> over HttpClient. Fetches the two raw
/// Home Assistant catalog payloads needed by P4-B discovery and never throws or
/// logs the token.
/// </summary>
public sealed class HttpHaCatalogReader : IHaCatalogReader
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public async Task<HaCatalogResult> ReadAsync(string url, string token, CancellationToken ct = default)
    {
        try
        {
            using var http = Client(url, token);
            using var states = await http.GetAsync("api/states", ct);
            if (states.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return new HaCatalogResult(HaProbeOutcome.Unauthorized);
            if (!states.IsSuccessStatusCode)
                return new HaCatalogResult(HaProbeOutcome.Unreachable);

            using var services = await http.GetAsync("api/services", ct);
            if (services.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return new HaCatalogResult(HaProbeOutcome.Unauthorized);
            if (!services.IsSuccessStatusCode)
                return new HaCatalogResult(HaProbeOutcome.Unreachable);

            var statesJson = await states.Content.ReadAsStringAsync(ct);
            var servicesJson = await services.Content.ReadAsStringAsync(ct);
            return new HaCatalogResult(HaProbeOutcome.Success, statesJson, servicesJson);
        }
        catch (Exception)
        {
            return new HaCatalogResult(HaProbeOutcome.Unreachable);
        }
    }

    private static HttpClient Client(string url, string token)
    {
        var http = new HttpClient { BaseAddress = new Uri(url), Timeout = Timeout };
        http.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return http;
    }
}
