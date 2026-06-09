namespace SmartNode.Services.HomeAssistant;

/// <summary>Raw HA catalog fetch result. Reuses HaProbeOutcome (Success/Unauthorized/Unreachable).</summary>
public sealed record HaCatalogResult(HaProbeOutcome Outcome, string? StatesJson = null, string? ServicesJson = null);

/// <summary>
/// Abstraction over the two raw HA reads discovery needs (GET api/states + api/services),
/// so the endpoint's connection/error mapping is testable with no network. The real
/// implementation is <c>HttpHaCatalogReader</c>. The token is used only as a Bearer header.
/// </summary>
public interface IHaCatalogReader
{
    Task<HaCatalogResult> ReadAsync(string url, string token, CancellationToken ct = default);
}
