using SmartNode.Services.Decisions;
using SmartNode.Models.MapeK;
using System.Text.Json;

namespace SmartNode.Services.Product;

public sealed record ProductDecisionEntry(
    string Timestamp,
    string? GoalId,
    string SelectedScenario,
    double? ObservedPriceNokPerKwh,
    IReadOnlyList<ProductDecisionAction> Actions,
    bool DryRun,
    string Explanation);

public sealed record ProductDecisionAction(
    string Domain,
    string Service,
    string EntityId,
    IReadOnlyDictionary<string, object?> Data,
    bool Executed);

internal static class ProductDecisionsEndpoint
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 100;
    private static readonly string[] CredentialMarkers =
    [
        "bearer",
        "token",
        "secret",
        "password",
        "api_key",
        "apikey",
        "access_token"
    ];

    public static ProductApiResult Get(IDecisionLog decisions, string? rawLimit)
    {
        ArgumentNullException.ThrowIfNull(decisions);

        var parsed = ParseLimit(rawLimit);
        if (parsed.StatusCode != 200)
        {
            return parsed;
        }

        var limit = (int)parsed.Payload!;
        var entries = decisions.GetRecent(limit)
            .Select(Sanitize)
            .ToList();
        return new ProductApiResult(200, new
        {
            count = entries.Count,
            limit,
            decisions = entries
        });
    }

    private static ProductApiResult ParseLimit(string? rawLimit)
    {
        if (string.IsNullOrWhiteSpace(rawLimit))
        {
            return new ProductApiResult(200, DefaultLimit);
        }

        if (!int.TryParse(rawLimit.Trim(), out var limit) || limit <= 0)
        {
            return new ProductApiResult(400, new { error = "limit must be a positive integer." });
        }

        return new ProductApiResult(200, Math.Min(limit, MaxLimit));
    }

    private static ProductDecisionEntry Sanitize(DecisionLogEntry entry)
        => new(
            Timestamp: SanitizeText(entry.Timestamp) ?? string.Empty,
            GoalId: SanitizeText(entry.GoalId),
            SelectedScenario: SanitizeText(entry.SelectedScenario) ?? string.Empty,
            ObservedPriceNokPerKwh: entry.ObservedPriceNokPerKwh,
            Actions: entry.Actions.Select(Sanitize).ToList(),
            DryRun: entry.DryRun,
            Explanation: SanitizeText(entry.Explanation) ?? string.Empty);

    private static ProductDecisionAction Sanitize(HaAction action)
        => new(
            Domain: SanitizeText(action.Domain) ?? string.Empty,
            Service: SanitizeText(action.Service) ?? string.Empty,
            EntityId: SanitizeText(action.EntityId) ?? string.Empty,
            Data: SanitizeData(action.Data),
            Executed: action.Executed);

    private static IReadOnlyDictionary<string, object?> SanitizeData(IReadOnlyDictionary<string, object?>? data)
    {
        var sanitized = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (data is null) return sanitized;

        var redactedIndex = 0;
        foreach (var (key, value) in data)
        {
            var safeKey = ContainsCredentialMarker(key)
                ? $"redacted_{++redactedIndex}"
                : key;
            sanitized[safeKey] = ContainsCredentialMarker(key) ? "[redacted]" : SanitizeValue(value);
        }
        return sanitized;
    }

    private static object? SanitizeValue(object? value)
        => value switch
        {
            null => null,
            string s => SanitizeText(s),
            JsonElement json => SanitizeJson(json),
            IReadOnlyDictionary<string, object?> dict => SanitizeData(dict),
            IDictionary<string, object?> dict => SanitizeData(
                dict.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal)),
            IEnumerable<object?> items => items.Select(SanitizeValue).ToList(),
            _ => value
        };

    private static object? SanitizeJson(JsonElement json)
    {
        return json.ValueKind switch
        {
            JsonValueKind.String => SanitizeText(json.GetString()),
            JsonValueKind.Number => json.TryGetInt64(out var l) ? l : json.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            JsonValueKind.Object => SanitizeJsonObject(json),
            JsonValueKind.Array => json.EnumerateArray().Select(SanitizeJson).ToList(),
            _ => null
        };
    }

    private static IReadOnlyDictionary<string, object?> SanitizeJsonObject(JsonElement json)
    {
        var sanitized = new Dictionary<string, object?>(StringComparer.Ordinal);
        var redactedIndex = 0;
        foreach (var prop in json.EnumerateObject())
        {
            var safeKey = ContainsCredentialMarker(prop.Name)
                ? $"redacted_{++redactedIndex}"
                : prop.Name;
            sanitized[safeKey] = ContainsCredentialMarker(prop.Name) ? "[redacted]" : SanitizeJson(prop.Value);
        }
        return sanitized;
    }

    private static string? SanitizeText(string? text)
        => ContainsCredentialMarker(text) ? "[redacted]" : text;

    private static bool ContainsCredentialMarker(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        return CredentialMarkers.Any(marker => text.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }
}
