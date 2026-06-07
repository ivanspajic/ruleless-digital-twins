namespace Logic.Mapek.Proactive;

public sealed class ProactiveAdvisory
{
    public bool ForecastAvailable { get; init; }
    public string? Warning { get; init; }
    public DateTimeOffset GeneratedAt { get; init; }
    public string Currency { get; init; } = "";
    public string Area { get; init; } = "";

    public double? CurrentPrice { get; init; }
    public double? Q1 { get; init; }   // cheap threshold (25th percentile of horizon)
    public double? Q3 { get; init; }   // peak threshold (75th percentile of horizon)

    public DateTimeOffset? NextPeakStart { get; init; }
    public double? NextPeakPrice { get; init; }
    public double? HoursUntilNextPeak { get; init; }

    public bool ShouldPreheat { get; init; }
    public bool ShouldDeferLoad { get; init; }
    public string Reason { get; init; } = "";
}

public interface IProactiveAdvisor
{
    // Triggers a fresh forecast read and recomputes the latest advisory.
    // Safe to call on every MAPE-K cycle: failures are logged and swallowed
    // so a missing/late HA forecast never breaks the loop.
    Task RefreshAsync(CancellationToken ct = default);

    // Last computed advisory, or null if RefreshAsync has never produced one.
    ProactiveAdvisory? Latest { get; }
}
