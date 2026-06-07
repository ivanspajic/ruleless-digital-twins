using Microsoft.Extensions.Logging;

namespace Logic.Mapek.Proactive;

// Read-only price-trend advisor for the proactive arm of MAPE-K.
// V1 is consultative: it computes whether right now is cheap/peak relative
// to the upcoming horizon, but never mutates OptimalConditions or the
// planner's decision. Consumers (UI, MAPE-K loop log, REST) read Latest.
public sealed class ProactiveAdvisor : IProactiveAdvisor
{
    private readonly IPriceForecastProvider _forecastProvider;
    private readonly ILogger<ProactiveAdvisor> _logger;
    private readonly object _lock = new();
    private ProactiveAdvisory? _latest;

    // How far ahead a peak must arrive to flag preheating. 6h gives the demo
    // enough lead time to be visible without being noise on a flat day.
    private const double PreheatLookaheadHours = 6.0;

    public ProactiveAdvisor(IPriceForecastProvider forecastProvider, ILogger<ProactiveAdvisor> logger)
    {
        _forecastProvider = forecastProvider;
        _logger = logger;
    }

    public ProactiveAdvisory? Latest {
        get { lock (_lock) { return _latest; } }
    }

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        try {
            var forecast = await _forecastProvider.GetForecastAsync(ct);
            var advisory = Compute(forecast);
            lock (_lock) { _latest = advisory; }

            if (advisory.ShouldPreheat || advisory.ShouldDeferLoad) {
                _logger.LogInformation("[PROACTIVE] {reason}", advisory.Reason);
            }
        } catch (Exception ex) {
            _logger.LogWarning(ex, "[PROACTIVE] Advisor refresh failed; keeping previous advisory");
        }
    }

    internal static ProactiveAdvisory Compute(PriceForecast forecast)
    {
        var now = DateTimeOffset.UtcNow;

        if (!forecast.Available || forecast.Slots.Count == 0) {
            return new ProactiveAdvisory {
                ForecastAvailable = false,
                Warning = forecast.Warning ?? "No price forecast available",
                GeneratedAt = now,
                Currency = forecast.Currency,
                Area = forecast.Area,
                Reason = "Forecast unavailable"
            };
        }

        // Restrict analysis to slots that haven't already elapsed.
        var future = forecast.Slots.Where(s => s.End > now).OrderBy(s => s.Start).ToList();
        if (future.Count == 0) {
            return new ProactiveAdvisory {
                ForecastAvailable = false,
                Warning = "All forecast slots are in the past",
                GeneratedAt = now,
                Currency = forecast.Currency,
                Area = forecast.Area,
                Reason = "Forecast stale"
            };
        }

        var prices = future.Select(s => s.Price).OrderBy(p => p).ToList();
        var q1 = Percentile(prices, 0.25);
        var q3 = Percentile(prices, 0.75);

        // Current slot = the one containing `now` (or the next one if we're between slots).
        var current = future.FirstOrDefault(s => s.Start <= now && now < s.End) ?? future.First();
        var currentPrice = current.Price;

        // Next peak = first future slot at or above Q3.
        var nextPeak = future.FirstOrDefault(s => s.Price >= q3);
        double? hoursUntilPeak = nextPeak is not null
            ? Math.Max(0, (nextPeak.Start - now).TotalHours)
            : (double?)null;

        bool shouldPreheat = currentPrice <= q1
            && nextPeak is not null
            && hoursUntilPeak is double h && h > 0 && h <= PreheatLookaheadHours;
        bool shouldDeferLoad = currentPrice >= q3;

        string reason;
        if (shouldPreheat) {
            reason = $"Cheap window now ({currentPrice:F3} {forecast.Currency} ≤ Q1 {q1:F3}); peak {nextPeak!.Price:F3} expected in {hoursUntilPeak:F1}h — consider preheating.";
        } else if (shouldDeferLoad) {
            reason = $"Currently in peak ({currentPrice:F3} {forecast.Currency} ≥ Q3 {q3:F3}) — consider deferring discretionary loads.";
        } else {
            reason = $"Price near median ({currentPrice:F3} {forecast.Currency}); no proactive action.";
        }

        return new ProactiveAdvisory {
            ForecastAvailable = true,
            GeneratedAt = now,
            Currency = forecast.Currency,
            Area = forecast.Area,
            CurrentPrice = currentPrice,
            Q1 = q1,
            Q3 = q3,
            NextPeakStart = nextPeak?.Start,
            NextPeakPrice = nextPeak?.Price,
            HoursUntilNextPeak = hoursUntilPeak,
            ShouldPreheat = shouldPreheat,
            ShouldDeferLoad = shouldDeferLoad,
            Reason = reason
        };
    }

    // Linear interpolation percentile on an already-sorted ascending list.
    private static double Percentile(IReadOnlyList<double> sorted, double p)
    {
        if (sorted.Count == 1) return sorted[0];
        var rank = p * (sorted.Count - 1);
        var lo = (int)Math.Floor(rank);
        var hi = (int)Math.Ceiling(rank);
        if (lo == hi) return sorted[lo];
        return sorted[lo] + (rank - lo) * (sorted[hi] - sorted[lo]);
    }
}
