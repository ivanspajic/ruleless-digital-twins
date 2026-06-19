using System.Net.Http.Headers;
using System.Text.Json;
using SmartNode.HaBindings;

namespace SmartNode.Validation
{
    public sealed record LiveHomeAssistantValidationSummary(
        string HaUrl,
        int HaEntitiesChecked,
        int HaEntitiesReachable,
        int HaServicesChecked,
        int HaServicesReachable);

    // Optional WP2 V2 live validation layer. It deliberately builds on the offline
    // BindingsValidator report instead of replacing it: JSON shape and structural
    // binding checks still run first, then this class adds Home Assistant reachability.
    public static class LiveHomeAssistantValidator
    {
        private static readonly Dictionary<string, string[]> RequiredServicesByDomain = new(StringComparer.OrdinalIgnoreCase) {
            { "input_boolean", new[] { "turn_on", "turn_off" } },
            { "light",         new[] { "turn_on", "turn_off" } },
            { "switch",        new[] { "turn_on", "turn_off" } },
            { "input_number",  new[] { "set_value" } },
            { "input_select",  new[] { "select_option" } }
        };

        private static readonly HashSet<string> RecognizedButUnsupportedActuatorDomains = new(StringComparer.OrdinalIgnoreCase) {
            "climate", "cover", "scene", "script"
        };

        public static async Task<LiveHomeAssistantValidationSummary> ValidateAsync(
            ValidationReport report,
            string bindingsPath,
            string haUrl,
            string? token,
            CancellationToken cancellationToken)
        {
            var normalizedHaUrl = NormalizeHaUrl(haUrl);
            var summary = new MutableSummary(normalizedHaUrl);

            if (!Uri.TryCreate(normalizedHaUrl, UriKind.Absolute, out var baseUri) ||
                (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps)) {
                report.Add(ValidationSeverity.Error, "HA_URL_INVALID",
                    "HA_URL must be an absolute http(s) URL for live Home Assistant validation.");
                return summary.ToRecord();
            }

            if (string.IsNullOrWhiteSpace(token)) {
                report.Add(ValidationSeverity.Error, "TOKEN_HA_MISSING",
                    "TOKEN_HA is not set; live Home Assistant validation cannot authenticate.");
                return summary.ToRecord();
            }

            HaBindingsConfig cfg;
            try {
                cfg = HaBindingsLoader.Load(bindingsPath);
            } catch {
                report.Add(ValidationSeverity.Error, "LIVE_SKIPPED_BINDINGS_LOAD",
                    "Live Home Assistant checks were skipped because the bindings file could not be loaded.");
                return summary.ToRecord();
            }

            using var http = new HttpClient { BaseAddress = baseUri, Timeout = TimeSpan.FromSeconds(5) };
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var entityIds = cfg.Sensors
                .Select((s, i) => new { Binding = s, Index = i })
                .Where(x => x.Binding.Kind == HaSensorImpl.HomeAssistant && !string.IsNullOrWhiteSpace(x.Binding.HaEntityId))
                .Select(x => new EntityReference("sensor", x.Index, x.Binding.SensorUri, x.Binding.HaEntityId!))
                .Concat(cfg.Actuators
                    .Select((a, i) => new { Binding = a, Index = i })
                    .Where(x => x.Binding.Kind == HaActuatorImpl.HomeAssistant && !string.IsNullOrWhiteSpace(x.Binding.HaEntityId))
                    .Select(x => new EntityReference("actuator", x.Index, x.Binding.ActuatorUri, x.Binding.HaEntityId!)))
                .ToList();

            summary.HaEntitiesChecked = entityIds.Count;
            var liveEntityIds = await FetchEntityIdsAsync(http, report, cancellationToken);
            if (liveEntityIds != null) {
                foreach (var reference in entityIds) {
                    if (liveEntityIds.Contains(reference.HaEntityId)) {
                        summary.HaEntitiesReachable++;
                    } else {
                        report.Add(ValidationSeverity.Error, "HA_ENTITY_NOT_FOUND",
                            $"{reference.Context} references haEntityId='{reference.HaEntityId}', but it was not found in Home Assistant /api/states.");
                    }
                }
            }

            var services = await FetchServicesAsync(http, report, cancellationToken);
            var serviceChecks = BuildServiceChecks(cfg, report);
            summary.HaServicesChecked = serviceChecks.Count;
            if (services != null) {
                foreach (var check in serviceChecks) {
                    if (services.TryGetValue(check.Domain, out var domainServices) &&
                        domainServices.Contains(check.Service)) {
                        summary.HaServicesReachable++;
                    } else {
                        report.Add(ValidationSeverity.Error, "HA_SERVICE_NOT_FOUND",
                            $"{check.Context} requires Home Assistant service '{check.Domain}.{check.Service}', but it was not found in /api/services.");
                    }
                }
            }

            return summary.ToRecord();
        }

        private static async Task<HashSet<string>?> FetchEntityIdsAsync(
            HttpClient http,
            ValidationReport report,
            CancellationToken cancellationToken)
        {
            try {
                using var response = await http.GetAsync("api/states", cancellationToken);
                if (!response.IsSuccessStatusCode) {
                    report.Add(ValidationSeverity.Error, "HA_STATES_UNAVAILABLE",
                        $"Home Assistant /api/states returned HTTP {(int)response.StatusCode}.");
                    return null;
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) {
                    report.Add(ValidationSeverity.Error, "HA_STATES_SHAPE",
                        "Home Assistant /api/states did not return the expected JSON array.");
                    return null;
                }

                var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in doc.RootElement.EnumerateArray()) {
                    if (item.TryGetProperty("entity_id", out var entityEl)) {
                        var id = entityEl.GetString();
                        if (!string.IsNullOrWhiteSpace(id)) ids.Add(id);
                    }
                }
                return ids;
            } catch (OperationCanceledException) {
                report.Add(ValidationSeverity.Error, "HA_STATES_TIMEOUT",
                    "Home Assistant /api/states did not respond before the live validation timeout.");
                return null;
            } catch (Exception ex) {
                report.Add(ValidationSeverity.Error, "HA_STATES_UNREACHABLE",
                    $"Home Assistant /api/states could not be reached: {ex.Message}");
                return null;
            }
        }

        private static async Task<Dictionary<string, HashSet<string>>?> FetchServicesAsync(
            HttpClient http,
            ValidationReport report,
            CancellationToken cancellationToken)
        {
            try {
                using var response = await http.GetAsync("api/services", cancellationToken);
                if (!response.IsSuccessStatusCode) {
                    report.Add(ValidationSeverity.Error, "HA_SERVICES_UNAVAILABLE",
                        $"Home Assistant /api/services returned HTTP {(int)response.StatusCode}.");
                    return null;
                }

                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.ValueKind != JsonValueKind.Array) {
                    report.Add(ValidationSeverity.Error, "HA_SERVICES_SHAPE",
                        "Home Assistant /api/services did not return the expected JSON array.");
                    return null;
                }

                var services = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var item in doc.RootElement.EnumerateArray()) {
                    if (!item.TryGetProperty("domain", out var domainEl)) continue;
                    var domain = domainEl.GetString();
                    if (string.IsNullOrWhiteSpace(domain)) continue;
                    var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    if (item.TryGetProperty("services", out var servicesEl) &&
                        servicesEl.ValueKind == JsonValueKind.Object) {
                        foreach (var service in servicesEl.EnumerateObject()) {
                            names.Add(service.Name);
                        }
                    }
                    services[domain] = names;
                }
                return services;
            } catch (OperationCanceledException) {
                report.Add(ValidationSeverity.Error, "HA_SERVICES_TIMEOUT",
                    "Home Assistant /api/services did not respond before the live validation timeout.");
                return null;
            } catch (Exception ex) {
                report.Add(ValidationSeverity.Error, "HA_SERVICES_UNREACHABLE",
                    $"Home Assistant /api/services could not be reached: {ex.Message}");
                return null;
            }
        }

        private static List<ServiceCheck> BuildServiceChecks(HaBindingsConfig cfg, ValidationReport report)
        {
            var checks = new List<ServiceCheck>();
            for (int i = 0; i < cfg.Actuators.Count; i++) {
                var actuator = cfg.Actuators[i];
                if (actuator.Kind != HaActuatorImpl.HomeAssistant ||
                    string.IsNullOrWhiteSpace(actuator.HaEntityId)) {
                    continue;
                }

                var context = $"actuators[{i}] ({actuator.ActuatorUri})";
                var domain = SplitEntityDomain(actuator.HaEntityId!);
                if (string.IsNullOrWhiteSpace(domain)) continue;

                if (RequiredServicesByDomain.TryGetValue(domain, out var services)) {
                    checks.AddRange(services.Select(service => new ServiceCheck(context, domain, service)));
                    continue;
                }

                if (RecognizedButUnsupportedActuatorDomains.Contains(domain)) {
                    report.Add(ValidationSeverity.Warning, "HA_ACTUATOR_SERVICE_UNSUPPORTED",
                        $"{context} uses domain '{domain}', which is recognised but not yet driven by HomeAssistantActuator.");
                } else {
                    report.Add(ValidationSeverity.Warning, "HA_ACTUATOR_SERVICE_UNMAPPED",
                        $"{context} uses domain '{domain}', which has no live service check mapping yet.");
                }
            }
            return checks;
        }

        private static string NormalizeHaUrl(string haUrl)
        {
            var url = string.IsNullOrWhiteSpace(haUrl) ? "http://localhost:8123/" : haUrl.Trim();
            return url.EndsWith("/", StringComparison.Ordinal) ? url : url + "/";
        }

        private static string SplitEntityDomain(string entityId)
        {
            var dot = entityId.IndexOf('.');
            return dot <= 0 ? "" : entityId[..dot];
        }

        private sealed record EntityReference(string Kind, int Index, string Uri, string HaEntityId)
        {
            public string Context => $"{Kind}s[{Index}] ({Uri})";
        }

        private sealed record ServiceCheck(string Context, string Domain, string Service);

        private sealed class MutableSummary
        {
            public string HaUrl { get; }
            public int HaEntitiesChecked { get; set; }
            public int HaEntitiesReachable { get; set; }
            public int HaServicesChecked { get; set; }
            public int HaServicesReachable { get; set; }

            public MutableSummary(string haUrl)
            {
                HaUrl = haUrl;
            }

            public LiveHomeAssistantValidationSummary ToRecord()
                => new(HaUrl, HaEntitiesChecked, HaEntitiesReachable, HaServicesChecked, HaServicesReachable);
        }
    }
}
