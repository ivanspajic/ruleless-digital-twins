using System.Diagnostics;
using System.Text.Json;
using Implementations.Actuators.HomeAssistant;
using Implementations.Actuators.RoomM370;
using Implementations.Sensors.Fakepool;
using Implementations.Sensors.HomeAssistant;
using Implementations.Sensors.RoomM370;
using Logic.TTComponentInterfaces;

namespace SmartNode.HaBindings
{
    // Builds HA sensor/actuator instances from a declarative JSON config so MAPE-K can be
    // pointed at any Home Assistant instance (showcase, testlab, ...) without recompiling
    // Factory.cs. Validation is strict (throws on malformed JSON) but missing HA entities
    // are only warned about — the existing HomeAssistantSensor.ObservePropertyValue
    // already returns 0.0 on failure so MAPE-K keeps running with degraded readings.
    public static class HaBindingsLoader
    {
        // Resolves the path of the bundled showcase bindings file when HA_BINDINGS_FILE is unset.
        // We try the working directory first (typical when launched via `dotnet run` from the repo
        // root), then climb from the assembly location to handle Debug/Release/published-binary
        // layouts where bin/Debug/net8.0 sits several levels under the repo. If neither candidate
        // exists, the cwd path is returned anyway so the caller's Load() raises the standard
        // "HA bindings file not found" error instead of a custom-resolved one — single source of
        // truth for missing-file diagnostics.
        public static string ResolveDefaultShowcasePath()
        {
            const string relPath = "config/ha-bindings.showcase.json";

            var cwdCandidate = Path.GetFullPath(relPath);
            if (File.Exists(cwdCandidate)) return cwdCandidate;

            try {
                var asmPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var dir = !string.IsNullOrEmpty(asmPath)
                    ? new DirectoryInfo(Path.GetDirectoryName(asmPath)!)
                    : null;
                for (int hops = 0; hops < 8 && dir != null; hops++, dir = dir.Parent) {
                    var candidate = Path.Combine(dir.FullName, "config", "ha-bindings.showcase.json");
                    if (File.Exists(candidate)) return candidate;
                }
            } catch { /* fall through to standard not-found error */ }

            return cwdCandidate;
        }

        public static HaBindingsConfig Load(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath)) {
                throw new ArgumentException("HA bindings file path is empty.", nameof(filePath));
            }
            if (!File.Exists(filePath)) {
                throw new FileNotFoundException($"HA bindings file not found: {filePath}", filePath);
            }

            var json = File.ReadAllText(filePath);
            HaBindingsConfig? cfg;
            try {
                cfg = JsonSerializer.Deserialize<HaBindingsConfig>(json, new JsonSerializerOptions {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });
            } catch (JsonException ex) {
                throw new InvalidDataException($"HA bindings file '{filePath}' is not valid JSON: {ex.Message}", ex);
            }

            if (cfg == null) {
                throw new InvalidDataException($"HA bindings file '{filePath}' parsed to null.");
            }

            ValidateOrThrow(cfg, filePath);
            return cfg;
        }

        private static void ValidateOrThrow(HaBindingsConfig cfg, string filePath)
        {
            for (int i = 0; i < cfg.Sensors.Count; i++) {
                var s = cfg.Sensors[i];
                var ctx = $"sensors[{i}] ({s.SensorUri})";
                if (string.IsNullOrWhiteSpace(s.SensorUri))    throw new InvalidDataException($"{filePath}: sensors[{i}].sensorUri is required.");
                if (string.IsNullOrWhiteSpace(s.ProcedureUri)) throw new InvalidDataException($"{filePath}: {ctx}.procedureUri is required.");
                switch (s.Kind) {
                    case HaSensorImpl.HomeAssistant:
                        if (string.IsNullOrWhiteSpace(s.HaEntityId))
                            throw new InvalidDataException($"{filePath}: {ctx}.haEntityId is required for kind=HomeAssistant.");
                        break;
                    case HaSensorImpl.Constant:
                    case HaSensorImpl.GeneralConstant:
                        if (s.ConstantValue == null)
                            throw new InvalidDataException($"{filePath}: {ctx}.constantValue is required for kind={s.Kind}.");
                        break;
                    case HaSensorImpl.DummyEnergy:
                    case HaSensorImpl.Fakepool:
                        // No extra fields needed.
                        break;
                    default:
                        throw new InvalidDataException($"{filePath}: {ctx}.kind={s.Kind} is unsupported.");
                }
            }

            for (int i = 0; i < cfg.Actuators.Count; i++) {
                var a = cfg.Actuators[i];
                var ctx = $"actuators[{i}] ({a.ActuatorUri})";
                if (string.IsNullOrWhiteSpace(a.ActuatorUri)) throw new InvalidDataException($"{filePath}: actuators[{i}].actuatorUri is required.");
                switch (a.Kind) {
                    case HaActuatorImpl.HomeAssistant:
                        if (string.IsNullOrWhiteSpace(a.HaEntityId))
                            throw new InvalidDataException($"{filePath}: {ctx}.haEntityId is required for kind=HomeAssistant.");
                        if (a.HaKind == null)
                            throw new InvalidDataException($"{filePath}: {ctx}.haKind is required for kind=HomeAssistant (one of InputBoolean, InputSelect, Light, Switch, InputNumber).");
                        break;
                    case HaActuatorImpl.DummyHeater:
                    case HaActuatorImpl.DummyFloorHeating:
                    case HaActuatorImpl.DummyDehumidifier:
                        // No extra fields needed.
                        break;
                    default:
                        throw new InvalidDataException($"{filePath}: {ctx}.kind={a.Kind} is unsupported.");
                }
            }
        }

        // Probe each declared HA entity_id once and warn (do not throw) if missing.
        // Best-effort: probe failures are swallowed because HA may simply be slow at startup.
        public static void WarnOnMissingEntities(HaBindingsConfig cfg, HttpClient haHttpClient)
        {
            var ids = cfg.Sensors.Where(s => s.Kind == HaSensorImpl.HomeAssistant && !string.IsNullOrWhiteSpace(s.HaEntityId))
                                 .Select(s => s.HaEntityId!)
                       .Concat(cfg.Actuators.Where(a => a.Kind == HaActuatorImpl.HomeAssistant && !string.IsNullOrWhiteSpace(a.HaEntityId))
                                            .Select(a => a.HaEntityId!))
                       .Distinct()
                       .ToList();

            foreach (var id in ids) {
                try {
                    using var probeCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                    var resp = haHttpClient.GetAsync($"api/states/{id}", probeCts.Token).GetAwaiter().GetResult();
                    if (!resp.IsSuccessStatusCode) {
                        Trace.WriteLine($"[HA bindings] WARNING entity_id {id} not found in HA states (HTTP {(int)resp.StatusCode}); MAPE-K may fail or use fallback values.");
                    }
                } catch (Exception ex) {
                    Trace.WriteLine($"[HA bindings] WARNING could not probe entity_id {id} ({ex.Message}); MAPE-K may fail or use fallback values.");
                }
            }
        }

        public static Dictionary<(string, string), ISensor> BuildSensorMap(HaBindingsConfig cfg, HttpClient haHttpClient)
        {
            var map = new Dictionary<(string, string), ISensor>();
            foreach (var s in cfg.Sensors) {
                ISensor impl = s.Kind switch {
                    HaSensorImpl.HomeAssistant     => new HomeAssistantSensor(s.SensorUri, s.HaEntityId!, s.Attribute, haHttpClient),
                    HaSensorImpl.Constant          => new ConstantSensor(s.SensorUri, s.ProcedureUri, s.ConstantValue!.Value),
                    HaSensorImpl.GeneralConstant   => new GeneralConstantSensor(s.SensorUri, s.ProcedureUri, s.ConstantValue!.Value),
                    HaSensorImpl.DummyEnergy       => new DummyEnergyConsumptionSensor(s.ProcedureUri, s.SensorUri),
                    HaSensorImpl.Fakepool          => new FakepoolSensor(s.SensorUri, s.ProcedureUri),
                    _ => throw new InvalidOperationException($"Unsupported sensor kind {s.Kind}")
                };
                map[(s.SensorUri, s.ProcedureUri)] = impl;
            }
            return map;
        }

        public static Dictionary<string, IActuator> BuildActuatorMap(HaBindingsConfig cfg, HttpClient haHttpClient)
        {
            var map = new Dictionary<string, IActuator>();
            foreach (var a in cfg.Actuators) {
                IActuator impl = a.Kind switch {
                    HaActuatorImpl.HomeAssistant       => new HomeAssistantActuator(a.ActuatorUri, a.HaEntityId!, a.HaKind!.Value, haHttpClient, a.OnOption),
                    HaActuatorImpl.DummyHeater         => new DummyHeater(a.ActuatorUri),
                    HaActuatorImpl.DummyFloorHeating   => new DummyFloorHeating(a.ActuatorUri),
                    HaActuatorImpl.DummyDehumidifier   => new DummyDehumidifier(a.ActuatorUri),
                    _ => throw new InvalidOperationException($"Unsupported actuator kind {a.Kind}")
                };
                map[a.ActuatorUri] = impl;
            }
            return map;
        }
    }
}
