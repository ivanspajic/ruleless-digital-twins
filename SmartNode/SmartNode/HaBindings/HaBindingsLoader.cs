using System.Text.Json;

namespace SmartNode.HaBindings
{
    public static class HaBindingsLoader
    {
        public static HaBindingsConfig Load(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
            {
                throw new ArgumentException("HA bindings file path is empty.", nameof(filePath));
            }
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"HA bindings file not found: {filePath}", filePath);
            }

            var json = File.ReadAllText(filePath);
            HaBindingsConfig? cfg;
            try
            {
                cfg = JsonSerializer.Deserialize<HaBindingsConfig>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });
            }
            catch (JsonException ex)
            {
                throw new InvalidDataException($"HA bindings file '{filePath}' is not valid JSON: {ex.Message}", ex);
            }

            if (cfg == null)
            {
                throw new InvalidDataException($"HA bindings file '{filePath}' parsed to null.");
            }

            Validate(cfg, filePath);
            return cfg;
        }

        private static void Validate(HaBindingsConfig cfg, string filePath)
        {
            for (int i = 0; i < cfg.Sensors.Count; i++)
            {
                var s = cfg.Sensors[i];
                if (string.IsNullOrWhiteSpace(s.SensorUri))
                    throw new InvalidDataException($"{filePath}: sensors[{i}] missing sensorUri.");
                if (string.IsNullOrWhiteSpace(s.ProcedureUri))
                    throw new InvalidDataException($"{filePath}: sensors[{i}] ({s.SensorUri}) missing procedureUri.");
                if (string.IsNullOrWhiteSpace(s.HaEntityId))
                    throw new InvalidDataException($"{filePath}: sensors[{i}] ({s.SensorUri}) missing haEntityId.");
            }

            for (int i = 0; i < cfg.Actuators.Count; i++)
            {
                var a = cfg.Actuators[i];
                if (string.IsNullOrWhiteSpace(a.ActuatorUri))
                    throw new InvalidDataException($"{filePath}: actuators[{i}] missing actuatorUri.");
                if (string.IsNullOrWhiteSpace(a.HaEntityId))
                    throw new InvalidDataException($"{filePath}: actuators[{i}] ({a.ActuatorUri}) missing haEntityId.");
            }
        }
    }
}
