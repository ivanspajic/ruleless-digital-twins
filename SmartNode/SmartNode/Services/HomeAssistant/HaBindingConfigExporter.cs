using System.Text.Json;
using System.Text.Json.Serialization;
using Implementations.Actuators.HomeAssistant;
using SmartNode.HaBindings;
using SmartNode.Validation;

namespace SmartNode.Services.HomeAssistant;

public sealed record HaBindingConfigExportResult(int StatusCode, object? Payload);

public sealed record HaBindingConfigExportCounts(
    [property: JsonPropertyName("sensors")] int Sensors,
    [property: JsonPropertyName("actuators")] int Actuators,
    [property: JsonPropertyName("skippedUnsupportedActuators")] int SkippedUnsupportedActuators,
    [property: JsonPropertyName("warnings")] int Warnings);

public sealed record HaBindingConfigExportDto(
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("exportKind")] string ExportKind,
    [property: JsonPropertyName("generatedAtUtc")] string GeneratedAtUtc,
    [property: JsonPropertyName("config")] HaBindingsConfig Config,
    [property: JsonPropertyName("validation")] ValidationReport Validation,
    [property: JsonPropertyName("warnings")] IReadOnlyList<string> Warnings,
    [property: JsonPropertyName("counts")] HaBindingConfigExportCounts Counts);

/// <summary>
/// Converts a review-only HA discovery draft into a runtime HA bindings config preview.
/// Pure by design: no file I/O, no HA reads, no service calls, and no token input.
/// </summary>
public static class HaBindingConfigExporter
{
    private const string Source = "homeassistant";
    private const string DraftKind = "ha-bindings.discovery-selection.draft";
    private const string ExportKind = "ha-bindings.runtime-config.preview";
    private const string DefaultProfile = "discovery-selection";
    private const string DefaultPlatform = "ha:HomeAssistantTest";
    private const string ValidationSource = "<ha-discovery-export>";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    public static HaBindingConfigExportResult ExportFromBody(string? rawBody, DateTimeOffset? generatedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
            return Bad("Request body is required.");

        try
        {
            using var doc = JsonDocument.Parse(rawBody, new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            });

            var draftElement = FindWrappedDraft(doc.RootElement);
            var draft = draftElement.Deserialize<HaBindingDraftDto>(JsonOptions);
            return Export(draft, generatedAtUtc);
        }
        catch (JsonException ex)
        {
            return Bad($"Invalid JSON: {ex.Message}");
        }
        catch (NotSupportedException ex)
        {
            return Bad($"Unsupported draft JSON shape: {ex.Message}");
        }
    }

    public static HaBindingConfigExportResult Export(HaBindingDraftDto? draft, DateTimeOffset? generatedAtUtc = null)
    {
        var headerError = ValidateDraftHeader(draft);
        if (headerError is not null)
            return Bad(headerError);

        var warnings = new List<string>();
        var config = new HaBindingsConfig
        {
            Profile = string.IsNullOrWhiteSpace(draft!.Profile) ? DefaultProfile : draft.Profile.Trim(),
            Platform = string.IsNullOrWhiteSpace(draft.Platform) ? DefaultPlatform : draft.Platform.Trim()
        };

        AddDraftWarnings(draft, warnings);
        AddIgnoredCandidateWarnings(draft, warnings);

        foreach (var observable in draft.Observables ?? Array.Empty<HaBindingDraftObservable>())
        {
            if (observable == null)
            {
                warnings.Add("Skipped observable: entry is null.");
                continue;
            }

            var binding = observable.Binding;
            if (binding == null ||
                string.IsNullOrWhiteSpace(binding.SensorUri) ||
                string.IsNullOrWhiteSpace(binding.ProcedureUri) ||
                string.IsNullOrWhiteSpace(binding.HaEntityId))
            {
                warnings.Add($"Skipped observable '{observable.EntityId}': binding requires sensorUri, procedureUri and haEntityId.");
                continue;
            }

            config.Sensors.Add(new HaSensorBinding
            {
                SensorUri = binding.SensorUri.Trim(),
                ProcedureUri = binding.ProcedureUri.Trim(),
                Kind = HaSensorImpl.HomeAssistant,
                HaEntityId = binding.HaEntityId.Trim()
            });
        }

        var skippedUnsupportedActuators = 0;
        foreach (var actuator in draft.Actuators ?? Array.Empty<HaBindingDraftActuator>())
        {
            if (actuator == null)
            {
                warnings.Add("Skipped actuator: entry is null.");
                continue;
            }

            var binding = actuator.Binding;
            if (!actuator.SupportedByRuntime)
            {
                skippedUnsupportedActuators++;
                warnings.Add($"Skipped actuator '{actuator.EntityId}': HA domain '{actuator.Domain}' is not currently supported by the runtime.");
                continue;
            }

            if (binding == null ||
                string.IsNullOrWhiteSpace(binding.ActuatorUri) ||
                string.IsNullOrWhiteSpace(binding.HaEntityId) ||
                string.IsNullOrWhiteSpace(binding.HaKind))
            {
                warnings.Add($"Skipped actuator '{actuator.EntityId}': binding requires actuatorUri, haEntityId and haKind.");
                continue;
            }

            if (!Enum.TryParse<HomeAssistantActuator.ActuatorKind>(binding.HaKind, ignoreCase: false, out var haKind))
            {
                warnings.Add($"Skipped actuator '{actuator.EntityId}': haKind '{binding.HaKind}' is not supported by HomeAssistantActuator.");
                continue;
            }

            config.Actuators.Add(new HaActuatorBinding
            {
                ActuatorUri = binding.ActuatorUri.Trim(),
                Kind = HaActuatorImpl.HomeAssistant,
                HaEntityId = binding.HaEntityId.Trim(),
                HaKind = haKind
            });
        }

        if (config.Sensors.Count == 0 && config.Actuators.Count == 0)
            return Bad("Draft does not contain any runtime-supported observables or actuators to export.");

        var validation = BindingsValidator.ValidateConfig(config, ValidationSource);
        var exportWarnings = warnings.Distinct(StringComparer.Ordinal).ToList();
        var payload = new HaBindingConfigExportDto(
            Source,
            ExportKind,
            (generatedAtUtc ?? DateTimeOffset.UtcNow).UtcDateTime.ToString("O"),
            config,
            validation,
            exportWarnings,
            new HaBindingConfigExportCounts(
                Sensors: config.Sensors.Count,
                Actuators: config.Actuators.Count,
                SkippedUnsupportedActuators: skippedUnsupportedActuators,
                Warnings: exportWarnings.Count));

        return new HaBindingConfigExportResult(200, payload);
    }

    private static string? ValidateDraftHeader(HaBindingDraftDto? draft)
    {
        if (draft == null) return "Draft is required.";

        if (!string.Equals(draft.Source, Source, StringComparison.Ordinal))
            return "Draft source must be 'homeassistant'.";

        if (!string.Equals(draft.DraftKind, DraftKind, StringComparison.Ordinal))
            return "draftKind must be 'ha-bindings.discovery-selection.draft'.";

        return null;
    }

    private static JsonElement FindWrappedDraft(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            throw new JsonException("Draft payload must be a JSON object.");

        foreach (var prop in root.EnumerateObject())
        {
            if (string.Equals(prop.Name, "draft", StringComparison.OrdinalIgnoreCase))
                return prop.Value;
        }

        return root;
    }

    private static void AddDraftWarnings(HaBindingDraftDto draft, List<string> warnings)
    {
        foreach (var warning in draft.Warnings ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(warning))
                warnings.Add("Draft warning: " + warning.Trim());
        }
    }

    private static void AddIgnoredCandidateWarnings(HaBindingDraftDto draft, List<string> warnings)
    {
        foreach (var ignored in draft.IgnoredCandidates ?? Array.Empty<HaBindingDraftIgnoredCandidate>())
        {
            if (ignored == null) continue;
            warnings.Add($"Ignored candidate '{ignored.EntityId}' ({ignored.Domain}): {ignored.Reason}");
        }
    }

    private static HaBindingConfigExportResult Bad(string error)
        => new(400, new { error });
}
