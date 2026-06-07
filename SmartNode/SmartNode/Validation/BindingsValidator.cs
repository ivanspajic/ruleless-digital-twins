using Implementations.Actuators.HomeAssistant;
using SmartNode.HaBindings;

namespace SmartNode.Validation
{
    // Static, offline validator for HA_BINDINGS_FILE. Wraps HaBindingsLoader.Load (so it inherits
    // the existing JSON-shape and required-fields checks) and adds structural cross-binding checks
    // that the loader does not perform: duplicate URIs, malformed entity IDs, and consistency
    // between Home Assistant domains and the C# ActuatorKind that handles them.
    //
    // Network probes against the live Home Assistant instance are intentionally out of scope here.
    // LiveHomeAssistantValidator builds on top of this static-analysis layer when ?live=true is
    // requested from the HTTP endpoint.
    public static class BindingsValidator
    {
        // The eleven HA domains the WP2 brief promises to recognise. Adding a new one here is the
        // single point of extension when SmartNode learns to drive a new HA domain.
        private static readonly HashSet<string> KnownDomains = new(StringComparer.OrdinalIgnoreCase) {
            "sensor", "binary_sensor",
            "light", "switch",
            "climate", "cover", "scene", "script",
            "input_number", "input_boolean", "input_select"
        };

        // Domains that the C# HomeAssistantActuator does not yet drive. Declaring a binding for
        // one of these is not a structural error — the binding can still feed downstream code —
        // but MAPE-K will not be able to actuate it through the existing actuator implementation.
        private static readonly HashSet<string> ActuatorDomainsNotYetSupported = new(StringComparer.OrdinalIgnoreCase) {
            "climate", "cover", "scene", "script"
        };

        // The exact one-to-one mapping between an HA domain and the C# ActuatorKind that handles
        // it inside HomeAssistantActuator. Anything outside this map is either a typo or a
        // not-yet-supported domain.
        private static readonly Dictionary<HomeAssistantActuator.ActuatorKind, string> ActuatorKindToDomain = new() {
            { HomeAssistantActuator.ActuatorKind.InputBoolean, "input_boolean" },
            { HomeAssistantActuator.ActuatorKind.InputSelect,  "input_select"  },
            { HomeAssistantActuator.ActuatorKind.Light,        "light"         },
            { HomeAssistantActuator.ActuatorKind.Switch,       "switch"        },
            { HomeAssistantActuator.ActuatorKind.InputNumber,  "input_number"  },
        };

        public static ValidationReport Validate(string bindingsPath)
        {
            var report = new ValidationReport(bindingsPath);

            HaBindingsConfig cfg;
            try {
                cfg = HaBindingsLoader.Load(bindingsPath);
            } catch (FileNotFoundException ex) {
                report.Add(ValidationSeverity.Error, "FILE_NOT_FOUND", ex.Message);
                return report;
            } catch (InvalidDataException ex) {
                // HaBindingsLoader wraps both malformed JSON and missing required fields under
                // InvalidDataException, so a single catch covers both surface failures.
                report.Add(ValidationSeverity.Error, "JSON_OR_SHAPE", ex.Message);
                return report;
            } catch (ArgumentException ex) {
                report.Add(ValidationSeverity.Error, "BAD_ARGUMENT", ex.Message);
                return report;
            }

            return ValidateConfig(cfg, bindingsPath);
        }

        public static ValidationReport ValidateConfig(HaBindingsConfig cfg, string sourceName = "<memory>")
        {
            var report = new ValidationReport(sourceName);

            report.Profile = cfg.Profile;
            report.SensorCount = cfg.Sensors.Count;
            report.ActuatorCount = cfg.Actuators.Count;

            CheckSensorRequiredFields(cfg, report);
            CheckActuatorRequiredFields(cfg, report);
            CheckSensorDuplicates(cfg, report);
            CheckActuatorDuplicates(cfg, report);
            CheckSensorEntityIds(cfg, report);
            CheckActuatorEntityIds(cfg, report);

            return report;
        }

        private static void CheckSensorRequiredFields(HaBindingsConfig cfg, ValidationReport report)
        {
            for (int i = 0; i < cfg.Sensors.Count; i++) {
                var s = cfg.Sensors[i];
                var ctx = $"sensors[{i}] ({s.SensorUri})";

                if (string.IsNullOrWhiteSpace(s.SensorUri)) {
                    report.Add(ValidationSeverity.Error, "SENSOR_URI_MISSING",
                        $"sensors[{i}].sensorUri is required.");
                }
                if (string.IsNullOrWhiteSpace(s.ProcedureUri)) {
                    report.Add(ValidationSeverity.Error, "SENSOR_PROCEDURE_URI_MISSING",
                        $"{ctx}.procedureUri is required.");
                }

                switch (s.Kind) {
                    case HaSensorImpl.HomeAssistant:
                        if (string.IsNullOrWhiteSpace(s.HaEntityId)) {
                            report.Add(ValidationSeverity.Error, "SENSOR_ENTITY_ID_MISSING",
                                $"{ctx}.haEntityId is required for kind=HomeAssistant.");
                        }
                        break;
                    case HaSensorImpl.Constant:
                    case HaSensorImpl.GeneralConstant:
                        if (s.ConstantValue == null) {
                            report.Add(ValidationSeverity.Error, "SENSOR_CONSTANT_VALUE_MISSING",
                                $"{ctx}.constantValue is required for kind={s.Kind}.");
                        }
                        break;
                }
            }
        }

        private static void CheckActuatorRequiredFields(HaBindingsConfig cfg, ValidationReport report)
        {
            for (int i = 0; i < cfg.Actuators.Count; i++) {
                var a = cfg.Actuators[i];
                var ctx = $"actuators[{i}] ({a.ActuatorUri})";

                if (string.IsNullOrWhiteSpace(a.ActuatorUri)) {
                    report.Add(ValidationSeverity.Error, "ACTUATOR_URI_MISSING",
                        $"actuators[{i}].actuatorUri is required.");
                }

                if (a.Kind != HaActuatorImpl.HomeAssistant) continue;

                if (string.IsNullOrWhiteSpace(a.HaEntityId)) {
                    report.Add(ValidationSeverity.Error, "ACTUATOR_ENTITY_ID_MISSING",
                        $"{ctx}.haEntityId is required for kind=HomeAssistant.");
                }
                if (a.HaKind == null) {
                    report.Add(ValidationSeverity.Error, "ACTUATOR_KIND_MISSING",
                        $"{ctx}.haKind is required for kind=HomeAssistant (one of InputBoolean, InputSelect, Light, Switch, InputNumber).");
                }
            }
        }

        private static void CheckSensorDuplicates(HaBindingsConfig cfg, ValidationReport report)
        {
            // Same (sensorUri, procedureUri) appearing twice would silently overwrite one entry
            // in HaBindingsLoader.BuildSensorMap, so it really is an error.
            var pairCounts = cfg.Sensors
                .GroupBy(s => (s.SensorUri, s.ProcedureUri))
                .Where(g => g.Count() > 1);
            foreach (var dup in pairCounts) {
                report.Add(ValidationSeverity.Error, "DUP_SENSOR_KEY",
                    $"sensorUri='{dup.Key.SensorUri}' + procedureUri='{dup.Key.ProcedureUri}' is declared {dup.Count()} times.");
            }

            // Reusing the same sensorUri across different procedures usually indicates a copy/paste
            // bug. We emit a warning rather than an error because a soft-sensor and a procedure can
            // legitimately share the URI in the upstream ontology.
            var uriCounts = cfg.Sensors
                .GroupBy(s => s.SensorUri)
                .Where(g => g.Count() > 1 && g.Select(x => x.ProcedureUri).Distinct().Count() > 1);
            foreach (var dup in uriCounts) {
                report.Add(ValidationSeverity.Warning, "DUP_SENSOR_URI",
                    $"sensorUri='{dup.Key}' is reused across {dup.Count()} different procedures.");
            }
        }

        private static void CheckActuatorDuplicates(HaBindingsConfig cfg, ValidationReport report)
        {
            var uriCounts = cfg.Actuators
                .GroupBy(a => a.ActuatorUri)
                .Where(g => g.Count() > 1);
            foreach (var dup in uriCounts) {
                report.Add(ValidationSeverity.Error, "DUP_ACTUATOR_URI",
                    $"actuatorUri='{dup.Key}' is declared {dup.Count()} times.");
            }

            // Two actuators pointing at the same HA entity_id is rarely intended (typically a
            // copy/paste from the showcase profile). It's not a structural error because MAPE-K
            // will still build distinct actuators, but it almost always means downstream commands
            // will fight each other.
            var idCounts = cfg.Actuators
                .Where(a => !string.IsNullOrWhiteSpace(a.HaEntityId))
                .GroupBy(a => a.HaEntityId!, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1);
            foreach (var dup in idCounts) {
                report.Add(ValidationSeverity.Warning, "DUP_ACTUATOR_ENTITY",
                    $"haEntityId='{dup.Key}' is targeted by {dup.Count()} actuators ({string.Join(", ", dup.Select(a => a.ActuatorUri))}).");
            }
        }

        private static void CheckSensorEntityIds(HaBindingsConfig cfg, ValidationReport report)
        {
            for (int i = 0; i < cfg.Sensors.Count; i++) {
                var s = cfg.Sensors[i];
                if (s.Kind != HaSensorImpl.HomeAssistant) continue;
                if (string.IsNullOrWhiteSpace(s.HaEntityId)) continue; // already caught by HaBindingsLoader

                var (domain, ok, _) = SplitEntityId(s.HaEntityId!);
                var ctx = $"sensors[{i}] ({s.SensorUri})";

                if (!ok) {
                    report.Add(ValidationSeverity.Error, "BAD_ENTITY_ID_SHAPE",
                        $"{ctx} haEntityId='{s.HaEntityId}' is not in the expected 'domain.object_id' shape.");
                    continue;
                }
                if (!KnownDomains.Contains(domain)) {
                    report.Add(ValidationSeverity.Warning, "UNKNOWN_DOMAIN",
                        $"{ctx} haEntityId='{s.HaEntityId}' uses domain '{domain}', which is not in the recognised set " +
                        "(sensor, binary_sensor, light, switch, climate, cover, scene, script, input_number, input_boolean, input_select).");
                }
            }
        }

        private static void CheckActuatorEntityIds(HaBindingsConfig cfg, ValidationReport report)
        {
            for (int i = 0; i < cfg.Actuators.Count; i++) {
                var a = cfg.Actuators[i];
                if (a.Kind != HaActuatorImpl.HomeAssistant) continue;
                if (string.IsNullOrWhiteSpace(a.HaEntityId)) continue; // already caught by HaBindingsLoader

                var ctx = $"actuators[{i}] ({a.ActuatorUri})";
                var (domain, ok, _) = SplitEntityId(a.HaEntityId!);

                if (!ok) {
                    report.Add(ValidationSeverity.Error, "BAD_ENTITY_ID_SHAPE",
                        $"{ctx} haEntityId='{a.HaEntityId}' is not in the expected 'domain.object_id' shape.");
                    continue;
                }
                if (!KnownDomains.Contains(domain)) {
                    report.Add(ValidationSeverity.Warning, "UNKNOWN_DOMAIN",
                        $"{ctx} haEntityId='{a.HaEntityId}' uses domain '{domain}', which is not in the recognised set.");
                    continue;
                }

                if (ActuatorDomainsNotYetSupported.Contains(domain)) {
                    report.Add(ValidationSeverity.Warning, "ACTUATOR_DOMAIN_UNSUPPORTED",
                        $"{ctx} haEntityId='{a.HaEntityId}' uses domain '{domain}', which is recognised but not yet driven by HomeAssistantActuator.");
                    continue;
                }

                // haKind is required for HA actuators (HaBindingsLoader already validates this);
                // we re-check defensively to keep this validator independent of loader internals.
                if (a.HaKind == null) {
                    report.Add(ValidationSeverity.Error, "ACTUATOR_KIND_MISSING",
                        $"{ctx} kind=HomeAssistant requires haKind (one of InputBoolean, InputSelect, Light, Switch, InputNumber).");
                    continue;
                }

                if (!ActuatorKindToDomain.TryGetValue(a.HaKind.Value, out var expectedDomain)) {
                    report.Add(ValidationSeverity.Error, "ACTUATOR_KIND_UNKNOWN",
                        $"{ctx} haKind='{a.HaKind}' is not one of the supported kinds.");
                    continue;
                }
                if (!string.Equals(domain, expectedDomain, StringComparison.OrdinalIgnoreCase)) {
                    report.Add(ValidationSeverity.Error, "ACTUATOR_KIND_DOMAIN_MISMATCH",
                        $"{ctx} haKind='{a.HaKind}' expects an entity in the '{expectedDomain}.*' domain but haEntityId='{a.HaEntityId}' is in the '{domain}.*' domain.");
                }
            }
        }

        // entity_id is normatively '<domain>.<object_id>' with a single dot separator (HA itself
        // refuses to register entities outside that shape). We don't try to be cleverer than that.
        private static (string Domain, bool Ok, string ObjectId) SplitEntityId(string entityId)
        {
            var dot = entityId.IndexOf('.');
            if (dot <= 0 || dot == entityId.Length - 1) return ("", false, "");
            var domain = entityId[..dot];
            var objectId = entityId[(dot + 1)..];
            if (objectId.Contains('.')) return (domain, false, objectId); // multiple dots → not a valid entity_id
            return (domain, true, objectId);
        }
    }
}
