using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using SmartNode.HaBindings;
using SmartNode.Validation;

namespace SmartNode.Services.Bindings
{
    public sealed record BindingsAdoptionEndpointResult(int StatusCode, object Payload);

    // Pure orchestration for POST /api/ha/bindings/adopt — DRY-RUN-ONLY slice (PR A2).
    //
    // It proves the adoption contract WITHOUT any side effect: no disk write, no
    // backup, no revision store, no HA_BINDINGS_FILE change, no Home Assistant call.
    // Real adoption (dryRun:false) is intentionally refused with 501 in this slice;
    // a later PR adds the real write behind HA_BINDINGS_ADOPTION_ENABLED.
    //
    // Check order (decided in the design review):
    //   400  empty body / invalid JSON / config missing / expectedCurrentHash missing
    //   501  dryRun:false (real adoption not implemented in this slice)
    //   409  stale write: expectedCurrentHash != current runtime config hash
    //   400  config sub-object unparseable
    //   422  validation FAIL, or WARN without acceptWarnings:true
    //   200  dry-run preview
    //
    // 409 is checked BEFORE validation on purpose: expectedCurrentHash is an
    // optimistic-concurrency guard (like If-Match), so a stale base is rejected
    // before the proposed edit is analysed.
    public static class BindingsAdoptionEndpoint
    {
        // Mirror HaBindingsLoader's parse options so the endpoint accepts exactly what
        // the loader would accept from a file (case-insensitive, trailing commas, // comments).
        private static readonly JsonSerializerOptions ParseOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        // Single hashing function used for both currentHash and adoptedHash, so a client
        // (or a test) can compute expectedCurrentHash exactly as the server computes
        // currentHash. Hashes raw UTF-8 bytes; never includes any secret.
        public static string Sha256Hex(string? raw)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw ?? string.Empty));
            return "sha256:" + Convert.ToHexString(bytes).ToLowerInvariant();
        }

        // currentConfigRaw: the active runtime bindings file content ("" when no file
        // exists). The caller (Program.cs) reads it from disk; this method stays pure
        // and offline-testable.
        public static BindingsAdoptionEndpointResult Adopt(string? rawBody, string currentConfigRaw)
        {
            if (string.IsNullOrWhiteSpace(rawBody))
                return new BindingsAdoptionEndpointResult(400, new { error = "Request body is required." });

            JsonDocument doc;
            try { doc = JsonDocument.Parse(rawBody); }
            catch (JsonException ex)
            {
                return new BindingsAdoptionEndpointResult(400, new { error = $"Invalid JSON: {ex.Message}" });
            }

            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    return new BindingsAdoptionEndpointResult(400, new { error = "Request body must be a JSON object." });

                if (!root.TryGetProperty("config", out var configEl) || configEl.ValueKind != JsonValueKind.Object)
                    return new BindingsAdoptionEndpointResult(400, new { error = "config (a JSON object) is required." });

                if (!root.TryGetProperty("expectedCurrentHash", out var hashEl)
                    || hashEl.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(hashEl.GetString()))
                    return new BindingsAdoptionEndpointResult(400, new { error = "expectedCurrentHash is required." });
                var expectedCurrentHash = hashEl.GetString()!;

                var profile = root.TryGetProperty("profile", out var pEl) && pEl.ValueKind == JsonValueKind.String
                    ? pEl.GetString()
                    : null;
                var acceptWarnings = root.TryGetProperty("acceptWarnings", out var awEl)
                    && awEl.ValueKind == JsonValueKind.True;

                // dryRun: absent => dry-run. Only an explicit `false` requests real adoption.
                var realRequested = root.TryGetProperty("dryRun", out var drEl)
                    && drEl.ValueKind == JsonValueKind.False;
                if (realRequested)
                    return new BindingsAdoptionEndpointResult(501, new
                    {
                        adopted = false,
                        dryRun = false,
                        error = "Real bindings adoption is not implemented in this slice. Use dryRun=true.",
                        reloadRequired = true,
                        warnings = Array.Empty<string>()
                    });

                var currentHash = Sha256Hex(currentConfigRaw);
                if (!string.Equals(expectedCurrentHash, currentHash, StringComparison.Ordinal))
                    return new BindingsAdoptionEndpointResult(409, new
                    {
                        adopted = false,
                        dryRun = true,
                        error = "Current bindings config has changed since the request was prepared.",
                        currentHash,
                        expectedCurrentHash,
                        reloadRequired = true,
                        warnings = new[] { "Reload the current bindings config and re-apply your edits." }
                    });

                var configRaw = configEl.GetRawText();
                HaBindingsConfig? cfg;
                try { cfg = JsonSerializer.Deserialize<HaBindingsConfig>(configRaw, ParseOptions); }
                catch (JsonException ex)
                {
                    return new BindingsAdoptionEndpointResult(400, new { error = $"Invalid config JSON: {ex.Message}" });
                }
                if (cfg is null)
                    return new BindingsAdoptionEndpointResult(400, new { error = "config parsed to null." });

                var report = BindingsValidator.ValidateConfig(cfg, profile ?? "<adopt>");
                var validation = ValidationDto(report);

                if (report.HasFailures)
                    return new BindingsAdoptionEndpointResult(422, new
                    {
                        adopted = false,
                        dryRun = true,
                        error = "Validation FAIL: the proposed bindings cannot be adopted.",
                        currentHash,
                        adoptedHash = Sha256Hex(configRaw),
                        validation,
                        reloadRequired = true,
                        warnings = Array.Empty<string>()
                    });

                if (report.WarningCount > 0 && !acceptWarnings)
                    return new BindingsAdoptionEndpointResult(422, new
                    {
                        adopted = false,
                        dryRun = true,
                        error = "Validation WARN: set acceptWarnings=true to adopt bindings with warnings.",
                        currentHash,
                        adoptedHash = Sha256Hex(configRaw),
                        validation,
                        reloadRequired = true,
                        warnings = Array.Empty<string>()
                    });

                return new BindingsAdoptionEndpointResult(200, new
                {
                    adopted = false,
                    dryRun = true,
                    profile,
                    currentHash,
                    adoptedHash = Sha256Hex(configRaw),
                    revisionId = (string?)null,
                    backupPath = (string?)null,
                    validation,
                    reloadRequired = true,
                    warnings = new[]
                    {
                        "Dry-run only: no file was written.",
                        "Restart would be required after real adoption."
                    }
                });
            }
        }

        // Stable, secrets-free projection of the report (same shape as the validate
        // endpoint). Severity is a string, not the enum ordinal.
        private static object ValidationDto(ValidationReport report) => new
        {
            status = report.Status,
            valid = !report.HasFailures,
            errorCount = report.ErrorCount,
            warningCount = report.WarningCount,
            profile = report.Profile,
            sensorCount = report.SensorCount,
            actuatorCount = report.ActuatorCount,
            issues = report.Issues.Select(i => new
            {
                severity = i.Severity.ToString(),
                code = i.Code,
                message = i.Message
            }).ToList()
        };
    }
}
