using System.Net;
using System.Text.Json;

namespace SmartNode.Services.HomeAssistant;

/// <summary>
/// Real <see cref="IHaProbe"/> over HttpClient. Maps 401/403 to Unauthorized, any
/// connect/timeout failure to Unreachable, and never throws. The token is used only
/// as a Bearer header — it is never logged.
/// </summary>
public sealed class HttpHaProbe : IHaProbe
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    public async Task<HaProbeResult> GetConfigAsync(string url, string token, CancellationToken ct = default)
    {
        try
        {
            using var http = Client(url, token);
            using var res = await http.GetAsync("api/config", ct);
            if (res.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                return new HaProbeResult(HaProbeOutcome.Unauthorized);
            if (!res.IsSuccessStatusCode)
                return new HaProbeResult(HaProbeOutcome.Unreachable);

            var body = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var version = root.TryGetProperty("version", out var v) ? v.GetString() : null;
            var location = root.TryGetProperty("location_name", out var l) ? l.GetString() : null;
            return new HaProbeResult(HaProbeOutcome.Success, version, location);
        }
        catch (Exception)
        {
            return new HaProbeResult(HaProbeOutcome.Unreachable);
        }
    }

    public async Task<int?> CountEntitiesAsync(string url, string token, CancellationToken ct = default)
    {
        try
        {
            using var http = Client(url, token);
            using var res = await http.GetAsync("api/states", ct);
            if (!res.IsSuccessStatusCode) return null;
            var body = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.ValueKind == JsonValueKind.Array
                ? doc.RootElement.GetArrayLength()
                : (int?)null;
        }
        catch (Exception)
        {
            return null;
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
