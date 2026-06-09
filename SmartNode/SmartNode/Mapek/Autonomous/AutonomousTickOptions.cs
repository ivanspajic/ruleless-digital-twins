using System.Globalization;

namespace SmartNode.Mapek.Autonomous;

// Parsed configuration for the optional autonomous MAPE-K tick (step 1.8).
// Disabled by default; the interval is clamped to a safe minimum so a stray
// MAPEK_TICK_INTERVAL_SECONDS=0 can never spin a tight loop against HA.
public sealed record AutonomousTickOptions(bool Enabled, TimeSpan Interval)
{
    public const string EnabledEnvVar = "MAPEK_AUTONOMOUS";
    public const string IntervalEnvVar = "MAPEK_TICK_INTERVAL_SECONDS";
    public const int DefaultIntervalSeconds = 60;
    public const int MinIntervalSeconds = 5;

    public static AutonomousTickOptions Parse(Func<string, string?> getEnv)
    {
        var rawEnabled = (getEnv(EnabledEnvVar) ?? string.Empty).Trim();
        var enabled = rawEnabled.Equals("1", StringComparison.OrdinalIgnoreCase)
            || rawEnabled.Equals("true", StringComparison.OrdinalIgnoreCase)
            || rawEnabled.Equals("yes", StringComparison.OrdinalIgnoreCase);

        // Absent or non-numeric → default; parsed but below the minimum (incl. 0
        // or negative) → clamped to the minimum.
        var seconds = DefaultIntervalSeconds;
        var rawInterval = getEnv(IntervalEnvVar);
        if (!string.IsNullOrWhiteSpace(rawInterval)
            && int.TryParse(rawInterval.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            seconds = parsed < MinIntervalSeconds ? MinIntervalSeconds : parsed;
        }

        return new AutonomousTickOptions(enabled, TimeSpan.FromSeconds(seconds));
    }
}
