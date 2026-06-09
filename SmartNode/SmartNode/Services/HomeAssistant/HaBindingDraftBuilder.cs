using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartNode.Services.HomeAssistant;

public sealed record HaBindingDraftResult(int StatusCode, object? Payload);

public sealed record HaBindingDraftCounts(
    [property: JsonPropertyName("selected")] int Selected,
    [property: JsonPropertyName("observables")] int Observables,
    [property: JsonPropertyName("actuators")] int Actuators,
    [property: JsonPropertyName("ignored")] int Ignored,
    [property: JsonPropertyName("missing")] int Missing,
    [property: JsonPropertyName("unsupportedActuators")] int UnsupportedActuators);

public sealed record HaBindingDraftBinding(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("haEntityId")] string HaEntityId,
    [property: JsonPropertyName("sensorUri")] string? SensorUri = null,
    [property: JsonPropertyName("procedureUri")] string? ProcedureUri = null,
    [property: JsonPropertyName("actuatorUri")] string? ActuatorUri = null,
    [property: JsonPropertyName("haKind")] string? HaKind = null);

public sealed record HaBindingDraftObservable(
    [property: JsonPropertyName("entityId")] string EntityId,
    [property: JsonPropertyName("friendlyName")] string FriendlyName,
    [property: JsonPropertyName("domain")] string Domain,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("binding")] HaBindingDraftBinding Binding);

public sealed record HaBindingDraftActuator(
    [property: JsonPropertyName("entityId")] string EntityId,
    [property: JsonPropertyName("friendlyName")] string FriendlyName,
    [property: JsonPropertyName("domain")] string Domain,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("services")] IReadOnlyList<string> Services,
    [property: JsonPropertyName("capabilities")] IReadOnlyDictionary<string, bool> Capabilities,
    [property: JsonPropertyName("supportedByRuntime")] bool SupportedByRuntime,
    [property: JsonPropertyName("binding")] HaBindingDraftBinding Binding);

public sealed record HaBindingDraftIgnoredCandidate(
    [property: JsonPropertyName("entityId")] string EntityId,
    [property: JsonPropertyName("domain")] string Domain,
    [property: JsonPropertyName("reason")] string Reason);

public sealed record HaBindingDraftDto(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("draftKind")] string DraftKind,
    [property: JsonPropertyName("generatedAtUtc")] string GeneratedAtUtc,
    [property: JsonPropertyName("profile")] string Profile,
    [property: JsonPropertyName("platform")] string Platform,
    [property: JsonPropertyName("observables")] IReadOnlyList<HaBindingDraftObservable> Observables,
    [property: JsonPropertyName("actuators")] IReadOnlyList<HaBindingDraftActuator> Actuators,
    [property: JsonPropertyName("ignoredCandidates")] IReadOnlyList<HaBindingDraftIgnoredCandidate> IgnoredCandidates,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string> Warnings,
    [property: JsonPropertyName("counts")] HaBindingDraftCounts Counts);

/// <summary>
/// Builds a review-only HA bindings draft from P4-B discovery output. It performs
/// no I/O, no HA calls, and never receives the HA token.
/// </summary>
public static class HaBindingDraftBuilder
{
    private const string Source = "homeassistant";
    private const string DraftKind = "ha-bindings.discovery-selection.draft";
    private const string Profile = "discovery-selection";
    private const string Platform = "ha:HomeAssistantTest";
    private const string OntologyBase = "http://www.semanticweb.org/rayan/ontologies/2025/ha/";

    private static readonly HashSet<string> ObservableDomains = new(StringComparer.Ordinal)
    {
        "sensor", "binary_sensor"
    };

    private static readonly HashSet<string> ActuatorDomains = new(StringComparer.Ordinal)
    {
        "climate", "light", "switch"
    };

    private static readonly Dictionary<string, string> RuntimeHaKindByDomain = new(StringComparer.Ordinal)
    {
        ["light"] = "Light",
        ["switch"] = "Switch",
        ["input_boolean"] = "InputBoolean",
        ["input_number"] = "InputNumber",
        ["input_select"] = "InputSelect",
    };

    public static HaBindingDraftResult Build(
        DiscoveryDto discovery,
        IEnumerable<string>? selectedEntityIds,
        DateTimeOffset? generatedAtUtc = null)
    {
        var selected = (selectedEntityIds ?? Array.Empty<string>())
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        if (selected.Count == 0)
            return new HaBindingDraftResult(400, new { error = "Select at least one discovered Home Assistant entity." });

        var byId = discovery.Groups
            .SelectMany(g => g.Entities.Select(e => (Group: g, Entity: e)))
            .ToDictionary(x => x.Entity.EntityId, x => x, StringComparer.Ordinal);

        var observables = new List<HaBindingDraftObservable>();
        var actuators = new List<HaBindingDraftActuator>();
        var ignored = new List<HaBindingDraftIgnoredCandidate>();
        var warnings = new List<string>();
        var missing = 0;
        var unsupportedActuators = 0;

        foreach (var entityId in selected)
        {
            if (!byId.TryGetValue(entityId, out var found))
            {
                missing++;
                warnings.Add($"Selected entity '{entityId}' was not present in the latest discovery result.");
                continue;
            }

            var domain = DomainOf(entityId);
            var role = found.Group.Role;
            if (string.Equals(role, "observable", StringComparison.Ordinal) || ObservableDomains.Contains(domain))
            {
                observables.Add(new HaBindingDraftObservable(
                    entityId,
                    found.Entity.FriendlyName,
                    domain,
                    found.Entity.State,
                    new HaBindingDraftBinding(
                        Kind: "HomeAssistant",
                        HaEntityId: entityId,
                        SensorUri: CandidateUri(entityId, "Sensor"),
                        ProcedureUri: CandidateUri(entityId, "Procedure"))));
                continue;
            }

            if (string.Equals(role, "actuator", StringComparison.Ordinal) || ActuatorDomains.Contains(domain))
            {
                RuntimeHaKindByDomain.TryGetValue(domain, out var haKind);
                var supported = !string.IsNullOrWhiteSpace(haKind);
                if (!supported)
                {
                    unsupportedActuators++;
                    warnings.Add(
                        $"Selected actuator '{entityId}' uses HA domain '{domain}', which is not currently driveable by HomeAssistantActuator.");
                }

                actuators.Add(new HaBindingDraftActuator(
                    entityId,
                    found.Entity.FriendlyName,
                    domain,
                    found.Entity.State,
                    found.Group.Services,
                    found.Group.Capabilities,
                    supported,
                    new HaBindingDraftBinding(
                        Kind: "HomeAssistant",
                        HaEntityId: entityId,
                        ActuatorUri: CandidateUri(entityId, "Actuator"),
                        HaKind: haKind)));
                continue;
            }

            var reason = $"HA domain '{domain}' is outside the P4-C observable/actuator draft scope.";
            ignored.Add(new HaBindingDraftIgnoredCandidate(entityId, domain, reason));
            warnings.Add($"Selected entity '{entityId}' ignored: {reason}");
        }

        var draft = new HaBindingDraftDto(
            Source,
            DraftKind,
            (generatedAtUtc ?? DateTimeOffset.UtcNow).UtcDateTime.ToString("O"),
            Profile,
            Platform,
            observables,
            actuators,
            ignored,
            warnings,
            new HaBindingDraftCounts(
                Selected: selected.Count,
                Observables: observables.Count,
                Actuators: actuators.Count,
                Ignored: ignored.Count,
                Missing: missing,
                UnsupportedActuators: unsupportedActuators));

        return new HaBindingDraftResult(200, draft);
    }

    public static async Task<HaBindingDraftResult> BuildAsync(
        string? rawBody,
        HaConnection store,
        IHaCatalogReader reader,
        CancellationToken ct = default)
    {
        var selected = ParseSelectedEntityIds(rawBody, out var error);
        if (error is not null)
            return new HaBindingDraftResult(400, new { error });

        var snap = store.GetSnapshot();
        if (!snap.TokenSet || string.IsNullOrWhiteSpace(snap.Token))
            return new HaBindingDraftResult(400, new { error = "Connect Home Assistant first." });

        var read = await reader.ReadAsync(snap.Url, snap.Token!, ct);
        switch (read.Outcome)
        {
            case HaProbeOutcome.Unauthorized:
                return new HaBindingDraftResult(401, new { error = "Home Assistant rejected the token (unauthorized)." });
            case HaProbeOutcome.Unreachable:
                return new HaBindingDraftResult(502, new { error = $"Home Assistant unreachable at {snap.Url}." });
        }

        var discovery = HaDiscoveryEndpoint.Build(read.StatesJson ?? "[]", read.ServicesJson ?? "[]");
        return Build(discovery, selected);
    }

    private static IReadOnlyList<string> ParseSelectedEntityIds(string? rawBody, out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            error = "Request body is required.";
            return Array.Empty<string>();
        }

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            if (!root.TryGetProperty("selectedEntityIds", out var ids) || ids.ValueKind != JsonValueKind.Array)
            {
                error = "selectedEntityIds must be a non-empty array.";
                return Array.Empty<string>();
            }

            var selected = new List<string>();
            foreach (var id in ids.EnumerateArray())
            {
                if (id.ValueKind != JsonValueKind.String)
                {
                    error = "selectedEntityIds must contain only strings.";
                    return Array.Empty<string>();
                }
                var value = id.GetString();
                if (!string.IsNullOrWhiteSpace(value)) selected.Add(value!);
            }

            if (selected.Count == 0)
            {
                error = "Select at least one discovered Home Assistant entity.";
                return Array.Empty<string>();
            }

            return selected;
        }
        catch (JsonException ex)
        {
            error = $"Invalid JSON: {ex.Message}";
            return Array.Empty<string>();
        }
    }

    private static string DomainOf(string entityId)
    {
        var dot = entityId.IndexOf('.');
        return dot <= 0 ? "" : entityId[..dot];
    }

    private static string CandidateUri(string entityId, string suffix)
    {
        var objectId = entityId[(entityId.IndexOf('.') + 1)..];
        var pascal = string.Concat(objectId
            .Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
        return OntologyBase + pascal + suffix;
    }
}
