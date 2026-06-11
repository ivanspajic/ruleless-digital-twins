using System.Globalization;

namespace SmartNode.Validation
{
    public enum ValidationSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed record ValidationIssue(ValidationSeverity Severity, string Code, string Message);

    // Validation report for a single bindings file. The report starts with the static,
    // offline checks from BindingsValidator and can be enriched by optional live checks.
    // The --validate-model CLI still consumes only the offline layer.
    public sealed class ValidationReport
    {
        public string SourcePath { get; }
        public string? Profile { get; set; }
        public int SensorCount { get; set; }
        public int ActuatorCount { get; set; }

        private readonly List<ValidationIssue> _issues = new();
        public IReadOnlyList<ValidationIssue> Issues => _issues;

        public ValidationReport(string sourcePath)
        {
            SourcePath = sourcePath;
        }

        public void Add(ValidationSeverity severity, string code, string message)
            => _issues.Add(new ValidationIssue(severity, code, message));

        public int ErrorCount   => _issues.Count(i => i.Severity == ValidationSeverity.Error);
        public int WarningCount => _issues.Count(i => i.Severity == ValidationSeverity.Warning);

        public bool HasFailures => ErrorCount > 0;

        // PASS = no issues at all; WARN = warnings only; FAIL = at least one error.
        public string Status => HasFailures ? "FAIL" : (WarningCount > 0 ? "WARN" : "PASS");

        public void PrintTo(TextWriter writer)
        {
            writer.WriteLine("[VALIDATE] Source:    {0}", SourcePath);
            writer.WriteLine("[VALIDATE] Profile:   {0}", Profile ?? "<none>");
            writer.WriteLine("[VALIDATE] Sensors:   {0}", SensorCount.ToString(CultureInfo.InvariantCulture));
            writer.WriteLine("[VALIDATE] Actuators: {0}", ActuatorCount.ToString(CultureInfo.InvariantCulture));
            writer.WriteLine();

            if (_issues.Count == 0) {
                writer.WriteLine("  (no issues)");
            } else {
                foreach (var issue in _issues.OrderByDescending(i => i.Severity)) {
                    var tag = issue.Severity switch {
                        ValidationSeverity.Error   => "[FAIL]",
                        ValidationSeverity.Warning => "[WARN]",
                        _                          => "[INFO]"
                    };
                    writer.WriteLine("  {0} {1}  {2}", tag, issue.Code, issue.Message);
                }
            }

            writer.WriteLine();
            writer.WriteLine(
                "Result: {0} ({1} error{2}, {3} warning{4})",
                Status,
                ErrorCount, ErrorCount == 1 ? "" : "s",
                WarningCount, WarningCount == 1 ? "" : "s");
        }
    }
}
