namespace SmartNode.Mapek.Analysis;

// Structured findings produced by the MAPE-K Analyzer phase (issue #59). The
// shape is deliberately minimal and stable so /api/mapek/tick consumers can
// switch on `code` and `severity` without parsing free-form `message` text.
public sealed record AnalysisFinding(
    string Code,
    string Severity,
    string Message
);

public sealed record AnalysisResult(
    IReadOnlyList<AnalysisFinding> Findings
);
