namespace SmartNode.Services.HomeAssistant;

public enum HaProbeOutcome { Success, Unauthorized, Unreachable }

/// <summary>Result of probing GET /api/config. On Success, version/location are populated.</summary>
public sealed record HaProbeResult(HaProbeOutcome Outcome, string? HaVersion = null, string? LocationName = null);

/// <summary>
/// Abstraction over the two Home Assistant calls the wizard needs, so the endpoint
/// can be tested with no network. The real implementation is <c>HttpHaProbe</c>.
/// </summary>
public interface IHaProbe
{
    /// <summary>Probe GET {url}api/config with the bearer token. Never throws — maps failures to an outcome.</summary>
    Task<HaProbeResult> GetConfigAsync(string url, string token, CancellationToken ct = default);

    /// <summary>Count entities via GET {url}api/states. Returns null on any failure (never throws).</summary>
    Task<int?> CountEntitiesAsync(string url, string token, CancellationToken ct = default);
}
