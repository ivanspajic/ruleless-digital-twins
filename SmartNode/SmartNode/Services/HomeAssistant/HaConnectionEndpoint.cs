using System.Text.Json;

namespace SmartNode.Services.HomeAssistant;

public sealed record HaConnectionResult(int StatusCode, object? Payload);

/// <summary>
/// Testable logic for the P4-A setup wizard endpoints. The HttpListener route in
/// Program.cs is a thin adapter that calls these and writes StatusCode + JSON(Payload).
/// Test-first, store-only-on-success: a failed probe never overwrites the runtime config.
/// The token is never logged here (no logger) and never echoed in any payload.
/// </summary>
public static class HaConnectionEndpoint
{
    public const string StatesWarning =
        "Connected to Home Assistant, but entity discovery (/api/states) failed.";

    public static async Task<HaConnectionResult> TestAndStoreAsync(
        string? rawBody, IHaProbe probe, HaConnection store, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
            return new HaConnectionResult(400, new { error = "Request body is required." });

        string? url = null, token = null;
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            // Validate the JSON type before GetString() so a non-string "url"/"token"
            // (number, bool, object…) yields a clean 400, never an unhandled 500.
            if (root.TryGetProperty("url", out var u))
            {
                if (u.ValueKind != JsonValueKind.String)
                    return new HaConnectionResult(400, new { error = "url must be a string" });
                url = u.GetString();
            }
            if (root.TryGetProperty("token", out var t))
            {
                if (t.ValueKind != JsonValueKind.String)
                    return new HaConnectionResult(400, new { error = "token must be a string" });
                token = t.GetString();
            }
        }
        catch (JsonException ex)
        {
            return new HaConnectionResult(400, new { error = $"Invalid JSON: {ex.Message}" });
        }

        if (string.IsNullOrWhiteSpace(url))
            return new HaConnectionResult(400, new { error = "'url' is required." });
        if (string.IsNullOrWhiteSpace(token))
            return new HaConnectionResult(400, new { error = "'token' is required." });

        var normUrl = HaConnection.Normalize(url);

        // 1) Test with the RECEIVED creds (not the stored ones).
        var probed = await probe.GetConfigAsync(normUrl, token, ct);
        switch (probed.Outcome)
        {
            case HaProbeOutcome.Unauthorized:
                return new HaConnectionResult(401,
                    new { error = "Home Assistant rejected the token (unauthorized)." });
            case HaProbeOutcome.Unreachable:
                return new HaConnectionResult(502,
                    new { error = $"Home Assistant unreachable at {normUrl}." });
        }

        // 2) Success → store (the only write path), then best-effort entity count.
        var count = await probe.CountEntitiesAsync(normUrl, token, ct);
        store.Update(normUrl, token, connected: true);

        if (count is null)
        {
            return new HaConnectionResult(200, new
            {
                ok = true,
                haVersion = probed.HaVersion,
                locationName = probed.LocationName,
                entityCount = (int?)null,
                warning = StatesWarning
            });
        }

        return new HaConnectionResult(200, new
        {
            ok = true,
            haVersion = probed.HaVersion,
            locationName = probed.LocationName,
            entityCount = count
        });
    }

    public static HaConnectionResult Status(HaConnection store)
    {
        // Single atomic read so url/tokenSet/connected/source are mutually consistent.
        var snap = store.GetSnapshot();
        return new HaConnectionResult(200, new
        {
            url = snap.Url,
            tokenSet = snap.TokenSet,
            connected = snap.LastConnected,
            source = snap.Source
        });
    }
}
