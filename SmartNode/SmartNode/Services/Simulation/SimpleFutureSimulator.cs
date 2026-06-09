using System.Globalization;
using SmartNode.Models.Goals;
using SmartNode.Models.Simulation;

namespace SmartNode.Services.Simulation;

// Deterministic heuristic simulator (step 1.4). NOT a physics simulation: it
// scores a small, fixed set of consultative scenarios from the observed state
// (room temperature + price) and the first active comfort goal, so the planner
// can select by argmax(Score) instead of blindly taking the first scenario.
public sealed class SimpleFutureSimulator : IFutureSimulator
{
    // Stable scenario identifiers consumed by the planner and by tests.
    public const string ScenarioDoNothing = "do-nothing";
    public const string ScenarioHeatNow = "heat-now";
    public const string ScenarioWaitCheaper = "wait-cheaper";
    public const string ScenarioBaseline = "no-goal-baseline";

    // At/above (target - tolerance) counts as already comfortable.
    private const double ComfortToleranceC = 0.1;

    public Task<IReadOnlyList<SimulationResult>> SimulateAsync(
        SimulationRequest request,
        CancellationToken cancellationToken = default)
    {
        var goal = request.ActiveGoals.FirstOrDefault(g =>
            string.Equals(g.Type, "comfort", StringComparison.OrdinalIgnoreCase));

        // No comfort goal to plan for → a single neutral baseline scenario.
        if (goal is null)
        {
            IReadOnlyList<SimulationResult> baseline = new[]
            {
                new SimulationResult(
                    ScenarioId: ScenarioBaseline,
                    Description: "No active comfort goal; nothing to plan.",
                    Score: 0.0,
                    Heuristic: true)
            };
            return Task.FromResult(baseline);
        }

        var target = goal.Objective.TargetTemperature;
        var currentTemp = TryReadRoomTemperature(
            request.ObservedState.HaEntitiesSnapshot, goal.Objective.Room);
        var price = request.ObservedState.CurrentPriceNokPerKwh;
        var maxPrice = goal.Constraints.MaxPriceNokPerKwh;

        var tempKnown = currentTemp.HasValue && target.HasValue;
        var alreadyComfortable = tempKnown && currentTemp!.Value >= target!.Value - ComfortToleranceC;
        var priceTooHigh = goal.Constraints.PreferLowPrice
            && maxPrice.HasValue && price.HasValue && price.Value > maxPrice.Value;

        var warnings = new List<string>();
        if (!tempKnown)
        {
            warnings.Add(
                $"Current temperature for room '{goal.Objective.Room}' unknown; " +
                "scoring assumes the comfort goal still applies.");
        }
        if (goal.Constraints.PreferLowPrice && !price.HasValue)
        {
            warnings.Add("Current price unknown; price constraint not enforced in scoring.");
        }
        IReadOnlyList<string>? w = warnings.Count > 0 ? warnings : null;

        // Deterministic, explainable scoring. Each branch is mutually steering so
        // exactly one scenario dominates per situation.
        var doNothingScore = alreadyComfortable ? 1.0 : 0.2;
        var heatNowScore = (alreadyComfortable ? 0.1 : 0.8) - (priceTooHigh ? 0.6 : 0.0);
        var waitScore = (!alreadyComfortable && priceTooHigh) ? 0.7 : 0.1;

        var tempText = currentTemp.HasValue
            ? currentTemp.Value.ToString("0.0", CultureInfo.InvariantCulture) + "°C"
            : "unknown";
        var targetText = target.HasValue
            ? target.Value.ToString("0.0", CultureInfo.InvariantCulture) + "°C"
            : "unset";
        var priceText = price.HasValue
            ? price.Value.ToString("0.00", CultureInfo.InvariantCulture)
            : "unknown";
        var limitText = maxPrice.HasValue
            ? maxPrice.Value.ToString("0.00", CultureInfo.InvariantCulture)
            : "unset";

        IReadOnlyList<SimulationResult> results = new[]
        {
            new SimulationResult(
                ScenarioId: ScenarioDoNothing,
                Description: $"Do nothing (current {tempText} vs target {targetText}).",
                Score: doNothingScore,
                Heuristic: true,
                Warnings: w),
            new SimulationResult(
                ScenarioId: ScenarioHeatNow,
                Description: priceTooHigh
                    ? $"Heat now toward {targetText}, but price {priceText} exceeds limit {limitText}."
                    : $"Heat now toward {targetText} (price {priceText} acceptable).",
                Score: heatNowScore,
                Heuristic: true,
                Warnings: w),
            new SimulationResult(
                ScenarioId: ScenarioWaitCheaper,
                Description: priceTooHigh
                    ? $"Wait for a cheaper window before heating (price {priceText} > limit {limitText})."
                    : "Wait for a cheaper window (only attractive when price is too high).",
                Score: waitScore,
                Heuristic: true,
                Warnings: w),
        };

        return Task.FromResult(results);
    }

    // Best-effort extraction of a room's current temperature from the flat HA
    // snapshot: first entry whose key mentions both the room and "temp" and whose
    // value parses as a number. Returns null when nothing matches.
    private static double? TryReadRoomTemperature(
        IReadOnlyDictionary<string, object?> snapshot, string? room)
    {
        if (snapshot.Count == 0) return null;

        var roomLower = room?.ToLowerInvariant();
        foreach (var kvp in snapshot)
        {
            var key = kvp.Key.ToLowerInvariant();
            if (!key.Contains("temp")) continue;
            if (!string.IsNullOrWhiteSpace(roomLower) && !key.Contains(roomLower!)) continue;
            if (TryParseDouble(kvp.Value, out var t)) return t;
        }
        return null;
    }

    private static bool TryParseDouble(object? value, out double result)
    {
        switch (value)
        {
            case double d: result = d; return true;
            case int i: result = i; return true;
            case long l: result = l; return true;
            case string s when double.TryParse(
                s, NumberStyles.Any, CultureInfo.InvariantCulture, out var p):
                result = p; return true;
            default: result = 0; return false;
        }
    }
}
