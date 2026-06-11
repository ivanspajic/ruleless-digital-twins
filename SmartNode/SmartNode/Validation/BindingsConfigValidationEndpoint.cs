using System.Text.Json;
using SmartNode.HaBindings;

namespace SmartNode.Validation
{
    public sealed record BindingsValidationEndpointResult(int StatusCode, object Payload);

    // Pure orchestration for POST /api/ha/bindings/validate (P4 — setup wizard).
    // It validates an *edited* binding config submitted in the request body, BEFORE
    // it is adopted/saved, so a non-developer can fix mistakes in the wizard instead
    // of editing files. No I/O, no live Home Assistant: it runs the same offline
    // static checks as the file validator (BindingsValidator.ValidateConfig).
    //
    // Status codes: 400 for an empty/unparseable body (cannot validate); 200 when
    // validation ran — the report itself (status PASS/WARN/FAIL) carries any
    // binding errors, exactly like the existing file-based validation endpoint.
    public static class BindingsConfigValidationEndpoint
    {
        // Mirror HaBindingsLoader's parse options so the endpoint accepts exactly
        // what the loader would accept from a file (case-insensitive, trailing
        // commas, // comments).
        private static readonly JsonSerializerOptions ParseOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip
        };

        public static BindingsValidationEndpointResult Validate(string? rawBody)
        {
            if (string.IsNullOrWhiteSpace(rawBody))
                return new BindingsValidationEndpointResult(400, new { error = "Request body is required." });

            HaBindingsConfig? cfg;
            try
            {
                cfg = JsonSerializer.Deserialize<HaBindingsConfig>(rawBody, ParseOptions);
            }
            catch (JsonException ex)
            {
                return new BindingsValidationEndpointResult(400, new { error = $"Invalid JSON: {ex.Message}" });
            }

            if (cfg is null)
                return new BindingsValidationEndpointResult(400, new { error = "Bindings body parsed to null." });

            var report = BindingsValidator.ValidateConfig(cfg, "<edited>");
            return new BindingsValidationEndpointResult(200, ToDto(report));
        }

        // Stable, secrets-free projection of the report for the wire. Severity is a
        // string (not the enum's int) so the dashboard does not depend on ordinals.
        private static object ToDto(ValidationReport report) => new
        {
            status = report.Status,                 // PASS | WARN | FAIL
            valid = !report.HasFailures,            // true unless there is >=1 error
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
