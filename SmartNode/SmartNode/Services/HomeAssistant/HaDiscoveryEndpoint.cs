using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartNode.Services.HomeAssistant;

// --- Discovery DTOs (camelCase via JsonPropertyName; dictionary keys stay literal) ---
public sealed record DiscoveryDomainSummary(
    [property: JsonPropertyName("domain")]      string Domain,
    [property: JsonPropertyName("role")]        string Role,
    [property: JsonPropertyName("entityCount")] int EntityCount);

public sealed record DiscoveryEntity(
    [property: JsonPropertyName("entityId")]      string EntityId,
    [property: JsonPropertyName("friendlyName")]  string FriendlyName,
    [property: JsonPropertyName("state")]         string State);

public sealed record DiscoveryGroup(
    [property: JsonPropertyName("domain")]        string Domain,
    [property: JsonPropertyName("role")]          string Role,
    [property: JsonPropertyName("services")]      IReadOnlyList<string> Services,
    [property: JsonPropertyName("capabilities")]  IReadOnlyDictionary<string, bool> Capabilities,
    [property: JsonPropertyName("entities")]      IReadOnlyList<DiscoveryEntity> Entities,
    [property: JsonPropertyName("warnings")]      IReadOnlyList<string> Warnings);

public sealed record DiscoveryDto(
    [property: JsonPropertyName("totalEntities")] int TotalEntities,
    [property: JsonPropertyName("domains")]       IReadOnlyList<DiscoveryDomainSummary> Domains,
    [property: JsonPropertyName("groups")]        IReadOnlyList<DiscoveryGroup> Groups,
    [property: JsonPropertyName("warnings")]      IReadOnlyList<string> Warnings);

// Thin adapter result (mirrors HaConnectionResult): StatusCode + JSON payload.
public sealed record HaDiscoveryResult(int StatusCode, object? Payload);

/// <summary>
/// P4-B Home Assistant discovery. Pure <see cref="Build"/> turns the raw HA /api/states +
/// /api/services payloads into a domain-level capability view (no per-entity capability,
/// no supported_features). The async overload adds connection/error mapping and is the
/// path Program.cs uses. The token is never read here and never appears in any payload.
/// </summary>
public static class HaDiscoveryEndpoint
{
    // Useful domains and their product role.
    private static readonly string[] ActuatorDomains = { "climate", "light", "switch" };
    private static readonly string[] ObservableDomains = { "sensor", "binary_sensor" };
    // Fixed render order for groups/summary.
    private static readonly string[] UsefulOrder = { "climate", "light", "switch", "sensor", "binary_sensor" };

    // Domain-level capability services we report (checked against /api/services).
    private static readonly Dictionary<string, string[]> CapabilityServices = new()
    {
        ["climate"] = new[] { "set_temperature", "set_hvac_mode", "turn_on", "turn_off" },
        ["light"]   = new[] { "turn_on", "turn_off" },
        ["switch"]  = new[] { "turn_on", "turn_off" },
    };

    // The one service whose absence raises a warning for an actuator domain that has entities.
    private static readonly Dictionary<string, string> PrimaryService = new()
    {
        ["climate"] = "set_temperature",
        ["light"]   = "turn_on",
        ["switch"]  = "turn_on",
    };

    private static string RoleOf(string domain) =>
        ActuatorDomains.Contains(domain) ? "actuator"
        : ObservableDomains.Contains(domain) ? "observable"
        : "other";

    /// <summary>Pure: build the discovery view from already-fetched raw HA JSON.</summary>
    public static DiscoveryDto Build(string statesJson, string servicesJson)
    {
        var entitiesByDomain = ParseStates(statesJson, out var totalEntities, out var otherCount);
        var servicesByDomain = ParseServices(servicesJson);

        // Summary: useful domains that have entities (fixed order) + a single "other" row if >0.
        var domains = new List<DiscoveryDomainSummary>();
        foreach (var d in UsefulOrder)
            if (entitiesByDomain.TryGetValue(d, out var list) && list.Count > 0)
                domains.Add(new DiscoveryDomainSummary(d, RoleOf(d), list.Count));
        if (otherCount > 0)
            domains.Add(new DiscoveryDomainSummary("other", "other", otherCount));

        // Groups: one per useful domain present (never "other").
        var groups = new List<DiscoveryGroup>();
        var topWarnings = new List<string>();
        foreach (var d in UsefulOrder)
        {
            if (!entitiesByDomain.TryGetValue(d, out var list) || list.Count == 0) continue;

            var role = RoleOf(d);
            var domainServices = servicesByDomain.TryGetValue(d, out var s) ? s : new HashSet<string>();

            // Services list: only meaningful for actuators; observables stay empty.
            var services = role == "actuator"
                ? domainServices.OrderBy(x => x, StringComparer.Ordinal).ToList()
                : new List<string>();

            // Capabilities: domain-level only.
            var caps = new Dictionary<string, bool>();
            if (role == "actuator" && CapabilityServices.TryGetValue(d, out var checks))
                foreach (var svc in checks) caps[svc] = domainServices.Contains(svc);

            // Warning: actuator with entities but missing its primary service.
            var warnings = new List<string>();
            if (role == "actuator" && PrimaryService.TryGetValue(d, out var primary)
                && !domainServices.Contains(primary))
            {
                var w = $"{d} entities present but service '{d}.{primary}' is not exposed by this instance.";
                warnings.Add(w);
                topWarnings.Add(w);
            }

            groups.Add(new DiscoveryGroup(d, role, services, caps, list, warnings));
        }

        return new DiscoveryDto(totalEntities, domains, groups, topWarnings);
    }

    /// <summary>
    /// Adapter path used by Program.cs. Reads creds atomically from the store, fetches the
    /// HA catalog via the reader, and maps outcomes to 400/401/502/200. Never returns the token.
    /// </summary>
    public static async Task<HaDiscoveryResult> BuildAsync(
        HaConnection store, IHaCatalogReader reader, CancellationToken ct = default)
    {
        var snap = store.GetSnapshot();
        if (!snap.TokenSet || string.IsNullOrWhiteSpace(snap.Token))
            return new HaDiscoveryResult(400, new { error = "Connect Home Assistant first." });

        var read = await reader.ReadAsync(snap.Url, snap.Token!, ct);
        switch (read.Outcome)
        {
            case HaProbeOutcome.Unauthorized:
                return new HaDiscoveryResult(401, new { error = "Home Assistant rejected the token (unauthorized)." });
            case HaProbeOutcome.Unreachable:
                return new HaDiscoveryResult(502, new { error = $"Home Assistant unreachable at {snap.Url}." });
        }

        var dto = Build(read.StatesJson ?? "[]", read.ServicesJson ?? "[]");
        return new HaDiscoveryResult(200, dto);
    }

    // --- parsing helpers (defensive: never throw on odd shapes) ---

    private static Dictionary<string, List<DiscoveryEntity>> ParseStates(
        string statesJson, out int total, out int otherCount)
    {
        var byDomain = new Dictionary<string, List<DiscoveryEntity>>();
        total = 0;
        otherCount = 0;

        using var doc = JsonDocument.Parse(statesJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return byDomain;

        foreach (var el in doc.RootElement.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object) continue;
            if (!el.TryGetProperty("entity_id", out var idEl) || idEl.ValueKind != JsonValueKind.String) continue;
            var entityId = idEl.GetString()!;
            var dot = entityId.IndexOf('.');
            if (dot <= 0) continue;
            var domain = entityId.Substring(0, dot);

            var state = el.TryGetProperty("state", out var st) && st.ValueKind == JsonValueKind.String
                ? st.GetString()! : "";

            var friendly = entityId;
            if (el.TryGetProperty("attributes", out var attrs) && attrs.ValueKind == JsonValueKind.Object
                && attrs.TryGetProperty("friendly_name", out var fn) && fn.ValueKind == JsonValueKind.String)
            {
                var name = fn.GetString();
                if (!string.IsNullOrWhiteSpace(name)) friendly = name!;
            }

            total++;
            if (RoleOf(domain) == "other") { otherCount++; continue; }

            if (!byDomain.TryGetValue(domain, out var list)) { list = new(); byDomain[domain] = list; }
            list.Add(new DiscoveryEntity(entityId, friendly, state));
        }
        return byDomain;
    }

    private static Dictionary<string, HashSet<string>> ParseServices(string servicesJson)
    {
        var map = new Dictionary<string, HashSet<string>>();
        using var doc = JsonDocument.Parse(servicesJson);
        if (doc.RootElement.ValueKind != JsonValueKind.Array) return map;

        foreach (var el in doc.RootElement.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object) continue;
            if (!el.TryGetProperty("domain", out var dEl) || dEl.ValueKind != JsonValueKind.String) continue;
            var domain = dEl.GetString()!;
            var names = new HashSet<string>(StringComparer.Ordinal);
            if (el.TryGetProperty("services", out var svc) && svc.ValueKind == JsonValueKind.Object)
                foreach (var prop in svc.EnumerateObject()) names.Add(prop.Name);
            map[domain] = names;
        }
        return map;
    }
}
