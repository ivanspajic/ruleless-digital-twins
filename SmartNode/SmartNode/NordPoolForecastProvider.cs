using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SmartNode;

// Auto-discovers the Home Assistant Nord Pool config_entry over WebSocket and
// fetches real future hourly prices via the nordpool.get_prices_for_date action.
// This is the first production path toward MAPE-K future-cost optimization:
// price forecast comes from Nord Pool, EV consumption forecast is simulated
// from candidate schedules. Later, general consumption should come from the
// digital twin/FMUs for each simulation path.
internal static class NordPoolForecastProvider
{
    public const string ProviderEnvVar = "PRICE_PROVIDER";
    public const string ProviderName = "homeassistant-nordpool";
    public const string ForecastSource = "homeassistant:nordpool.get_prices_for_date";

    public sealed record Slot(DateTimeOffset Start, DateTimeOffset End, int HourLocal, double Price);

    public sealed class Forecast
    {
        public bool ForecastAvailable { get; init; }
        public string Source { get; init; } = ForecastSource;
        public bool ConfigEntryDiscovered { get; init; }
        public string? ConfigEntryId { get; init; }
        public string Area { get; init; } = "";
        public string Currency { get; init; } = "";
        public string Timezone { get; init; } = "";
        public IReadOnlyList<Slot> Slots { get; init; } = Array.Empty<Slot>();
        public string? Warning { get; init; }
    }

    private static string? _cachedEntryId;
    private static readonly SemaphoreSlim _entryLock = new(1, 1);

    public static string GetConfiguredProvider()
    {
        var raw = Environment.GetEnvironmentVariable(ProviderEnvVar);
        if (string.IsNullOrWhiteSpace(raw)) return ProviderName;

        var normalized = raw.Trim().ToLowerInvariant();
        if (normalized == ProviderName) return ProviderName;
        if (normalized == ReplayPriceForecastProvider.ProviderName) return ReplayPriceForecastProvider.ProviderName;

        // fakepool is explicitly listed in the roadmap as a future work name and
        // is intentionally NOT implemented in this build. Reject it loudly so we
        // never silently fall back to fake prices.
        if (normalized == "fakepool")
        {
            throw new InvalidOperationException(
                $"{ProviderEnvVar}='{raw}' is not implemented in this build. " +
                $"Supported values: '{ProviderName}' (default, real Home Assistant Nord Pool) or " +
                $"'{ReplayPriceForecastProvider.ProviderName}' (deterministic replay from {ReplayPriceForecastProvider.ReplayFileEnvVar}). " +
                "There is no silent fallback to fake providers.");
        }

        throw new InvalidOperationException(
            $"{ProviderEnvVar}='{raw}' is unsupported. " +
            $"Supported values: '{ProviderName}' (default) or '{ReplayPriceForecastProvider.ProviderName}'. " +
            "There is no silent fallback to fake or replay providers.");
    }

    public static string GetHaUrl()
        => Environment.GetEnvironmentVariable("HA_URL") ?? "http://localhost:8123/";

    public static string GetArea()
        => Environment.GetEnvironmentVariable("HA_NORDPOOL_AREA") ?? "NO5";

    public static string GetCurrency()
        => Environment.GetEnvironmentVariable("HA_NORDPOOL_CURRENCY") ?? "NOK";

    public static string GetTimezone()
        => Environment.GetEnvironmentVariable("HA_TIMEZONE") ?? "Europe/Oslo";

    public static int GetHorizonHours()
    {
        var raw = Environment.GetEnvironmentVariable("OPTIMIZATION_HORIZON_HOURS");
        return int.TryParse(raw, out var h) && h > 0 ? h : 24;
    }

    public static async Task<string> DiscoverNordPoolConfigEntryAsync(string token, string haUrl, ILogger logger, CancellationToken ct = default)
    {
        var overrideEntry = Environment.GetEnvironmentVariable("HA_NORDPOOL_CONFIG_ENTRY");
        if (!string.IsNullOrWhiteSpace(overrideEntry))
        {
            logger.LogInformation("NordPool: using HA_NORDPOOL_CONFIG_ENTRY override = {entry}", overrideEntry);
            return overrideEntry;
        }

        await _entryLock.WaitAsync(ct);
        try
        {
            if (!string.IsNullOrWhiteSpace(_cachedEntryId)) return _cachedEntryId!;

            var wsUri = ToWebSocketUri(haUrl);
            using var ws = new ClientWebSocket();
            await ws.ConnectAsync(wsUri, ct);

            await ExpectAsync(ws, msg =>
                msg.RootElement.TryGetProperty("type", out var t) && t.GetString() == "auth_required", ct);

            await SendAsync(ws, new { type = "auth", access_token = token }, ct);

            await ExpectAsync(ws, msg =>
                msg.RootElement.TryGetProperty("type", out var t) && t.GetString() == "auth_ok", ct);

            // Primary path: config_entries/get filtered by domain.
            await SendAsync(ws, new { id = 1, type = "config_entries/get", domain = "nordpool" }, ct);
            using (var resp = await ReceiveAsync(ws, ct))
            {
                var entryId = ExtractFirstEntryId(resp.RootElement);
                if (!string.IsNullOrEmpty(entryId))
                {
                    _cachedEntryId = entryId;
                    logger.LogInformation("NordPool: discovered config_entry {entry} via config_entries/get", entryId);
                    return entryId;
                }
            }

            // Fallback: scan the entity registry for a Nord Pool entity, then read its config_entry_id.
            await SendAsync(ws, new { id = 2, type = "config/entity_registry/list" }, ct);
            using (var resp = await ReceiveAsync(ws, ct))
            {
                var entryId = ExtractEntryFromEntityRegistry(resp.RootElement);
                if (!string.IsNullOrEmpty(entryId))
                {
                    _cachedEntryId = entryId;
                    logger.LogInformation("NordPool: discovered config_entry {entry} via entity_registry fallback", entryId);
                    return entryId;
                }
            }

            throw new InvalidOperationException(
                "Nord Pool config entry could not be discovered. Make sure the Nord Pool integration is configured in Home Assistant.");
        }
        finally
        {
            _entryLock.Release();
        }
    }

    public static async Task<Forecast> GetForecastAsync(string token, ILogger logger, CancellationToken ct = default)
    {
        var haUrl = GetHaUrl();
        var area = GetArea();
        var currency = GetCurrency();
        var tzId = GetTimezone();
        var horizon = GetHorizonHours();

        string entryId;
        bool discovered;
        try
        {
            entryId = await DiscoverNordPoolConfigEntryAsync(token, haUrl, logger, ct);
            discovered = true;
        }
        catch (Exception ex)
        {
            logger.LogWarning("NordPool: discovery failed: {msg}", ex.Message);
            return new Forecast
            {
                ForecastAvailable = false,
                ConfigEntryDiscovered = false,
                Area = area,
                Currency = currency,
                Timezone = tzId,
                Warning = ex.Message
            };
        }

        var tz = ResolveTimezone(tzId);
        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, tz);

        using var http = new HttpClient { BaseAddress = new Uri(NormalizeBase(haUrl)), Timeout = TimeSpan.FromSeconds(8) };
        http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var slots = new List<Slot>();
        // Today is always required; tomorrow may not be published yet (typically before ~13:00 Europe/Oslo).
        var todayStr = nowLocal.ToString("yyyy-MM-dd");
        var tomorrowStr = nowLocal.AddDays(1).ToString("yyyy-MM-dd");

        await TryFetchDay(http, entryId, area, todayStr, tz, slots, logger, ct);
        await TryFetchDay(http, entryId, area, tomorrowStr, tz, slots, logger, ct);

        // Keep only future slots within the horizon window. `horizon` is in HOURS,
        // not slot count — Nord Pool can return 15-min sub-hourly slots (96/day),
        // so a count-based Take(24) would only cover ~6 hours.
        var horizonEnd = nowLocal.AddHours(horizon);
        var futureSlots = slots
            .Where(s => s.End > nowLocal && s.Start < horizonEnd)
            .GroupBy(s => s.Start)
            .Select(g => g.First())
            .OrderBy(s => s.Start)
            .ToList();

        if (futureSlots.Count == 0)
        {
            return new Forecast
            {
                ForecastAvailable = false,
                ConfigEntryDiscovered = discovered,
                ConfigEntryId = entryId,
                Area = area,
                Currency = currency,
                Timezone = tzId,
                Warning = "Nord Pool returned no future slots for area " + area
            };
        }

        return new Forecast
        {
            ForecastAvailable = true,
            ConfigEntryDiscovered = discovered,
            ConfigEntryId = entryId,
            Area = area,
            Currency = currency,
            Timezone = tzId,
            Slots = futureSlots
        };
    }

    private static async Task TryFetchDay(HttpClient http, string entryId, string area, string dateLocal, TimeZoneInfo tz, List<Slot> slots, ILogger logger, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new { config_entry = entryId, date = dateLocal });
        try
        {
            using var resp = await http.PostAsync(
                "api/services/nordpool/get_prices_for_date?return_response",
                new StringContent(payload, Encoding.UTF8, "application/json"), ct);

            if (!resp.IsSuccessStatusCode)
            {
                logger.LogInformation("NordPool: get_prices_for_date {date} returned {code}", dateLocal, (int)resp.StatusCode);
                return;
            }

            var raw = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(raw);

            // Response shape: { "service_response": { "<AREA>": [ { "start", "end", "price" }, ... ] } }
            // Some HA versions nest differently; we accept either { service_response: {...} } or the direct map.
            JsonElement root = doc.RootElement;
            JsonElement areaArray = default;
            bool found = false;

            // Case-insensitive area match — HA may return "no5" instead of "NO5"
            if (root.TryGetProperty("service_response", out var sr))
            {
                foreach (var prop in sr.EnumerateObject())
                {
                    if (string.Equals(prop.Name, area, StringComparison.OrdinalIgnoreCase)
                        && prop.Value.ValueKind == JsonValueKind.Array)
                    { areaArray = prop.Value; found = true; break; }
                }
            }
            if (!found)
            {
                foreach (var prop in root.EnumerateObject())
                {
                    if (string.Equals(prop.Name, area, StringComparison.OrdinalIgnoreCase)
                        && prop.Value.ValueKind == JsonValueKind.Array)
                    { areaArray = prop.Value; found = true; break; }
                }
            }

            if (!found)
            {
                var preview = raw.Length > 600 ? raw[..600] : raw;
                logger.LogWarning("NordPool: {date} area={area} not found in response. Body: {body}", dateLocal, area, preview);
                return;
            }

            int before = slots.Count;
            foreach (var item in areaArray.EnumerateArray())
            {
                if (!item.TryGetProperty("start", out var sEl)) continue;
                if (!item.TryGetProperty("end", out var eEl)) continue;
                if (!item.TryGetProperty("price", out var pEl)) continue;

                var startUtc = ParseDate(sEl.GetString());
                var endUtc = ParseDate(eEl.GetString());
                if (startUtc is null || endUtc is null) continue;

                // get_prices_for_date returns price-per-MWh in the integration currency; convert to /kWh.
                double rawPrice = pEl.ValueKind == JsonValueKind.Number ? pEl.GetDouble() : 0;
                double pricePerKwh = rawPrice / 1000.0;

                var startLocal = TimeZoneInfo.ConvertTime(startUtc.Value, tz);
                var endLocal = TimeZoneInfo.ConvertTime(endUtc.Value, tz);

                slots.Add(new Slot(startLocal, endLocal, startLocal.Hour, pricePerKwh));
            }
            logger.LogInformation("NordPool: {date} → {n} slots added (total={t})", dateLocal, slots.Count - before, slots.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning("NordPool: get_prices_for_date {date} failed: {msg}", dateLocal, ex.Message);
        }
    }

    private static Uri ToWebSocketUri(string haUrl)
    {
        var b = NormalizeBase(haUrl);
        var u = new Uri(b);
        var scheme = u.Scheme == "https" ? "wss" : "ws";
        return new Uri($"{scheme}://{u.Authority}/api/websocket");
    }

    private static string NormalizeBase(string haUrl)
    {
        if (string.IsNullOrWhiteSpace(haUrl)) return "http://localhost:8123/";
        return haUrl.EndsWith('/') ? haUrl : haUrl + "/";
    }

    private static async Task SendAsync(ClientWebSocket ws, object payload, CancellationToken ct)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));
        await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
    }

    private static async Task<JsonDocument> ReceiveAsync(ClientWebSocket ws, CancellationToken ct)
    {
        var buf = new byte[16 * 1024];
        using var ms = new MemoryStream();
        WebSocketReceiveResult res;
        do
        {
            res = await ws.ReceiveAsync(buf, ct);
            ms.Write(buf, 0, res.Count);
        } while (!res.EndOfMessage);
        ms.Position = 0;
        return JsonDocument.Parse(ms);
    }

    private static async Task ExpectAsync(ClientWebSocket ws, Func<JsonDocument, bool> predicate, CancellationToken ct)
    {
        // Skip benign messages until we get the one we want, or fail loudly.
        for (int i = 0; i < 8; i++)
        {
            using var doc = await ReceiveAsync(ws, ct);
            if (predicate(doc)) return;
            if (doc.RootElement.TryGetProperty("type", out var t) && t.GetString() == "auth_invalid")
                throw new InvalidOperationException("Home Assistant rejected TOKEN_HA (auth_invalid).");
        }
        throw new InvalidOperationException("Did not receive expected WebSocket message from Home Assistant.");
    }

    private static string? ExtractFirstEntryId(JsonElement root)
    {
        // Standard shape: { id, type:"result", success:true, result:[ { entry_id, ..., disabled_by, ... }, ... ] }
        if (!root.TryGetProperty("result", out var result)) return null;
        if (result.ValueKind != JsonValueKind.Array) return null;

        foreach (var entry in result.EnumerateArray())
        {
            if (entry.TryGetProperty("disabled_by", out var d) && d.ValueKind != JsonValueKind.Null) continue;

            if (entry.TryGetProperty("entry_id", out var idEl) && idEl.ValueKind == JsonValueKind.String)
                return idEl.GetString();
            if (entry.TryGetProperty("id", out var idEl2) && idEl2.ValueKind == JsonValueKind.String)
                return idEl2.GetString();
        }
        return null;
    }

    private static string? ExtractEntryFromEntityRegistry(JsonElement root)
    {
        if (!root.TryGetProperty("result", out var result)) return null;
        if (result.ValueKind != JsonValueKind.Array) return null;

        foreach (var entity in result.EnumerateArray())
        {
            string? platform = entity.TryGetProperty("platform", out var p) ? p.GetString() : null;
            string? entityId = entity.TryGetProperty("entity_id", out var e) ? e.GetString() : null;
            string? cfgEntry = entity.TryGetProperty("config_entry_id", out var c) && c.ValueKind == JsonValueKind.String
                ? c.GetString() : null;

            if (string.IsNullOrEmpty(cfgEntry)) continue;
            bool platformMatch = string.Equals(platform, "nordpool", StringComparison.OrdinalIgnoreCase);
            bool entityMatch = entityId != null && entityId.Contains("nord_pool", StringComparison.OrdinalIgnoreCase);
            if (platformMatch || entityMatch) return cfgEntry;
        }
        return null;
    }

    private static DateTimeOffset? ParseDate(string? s)
    {
        if (string.IsNullOrEmpty(s)) return null;
        return DateTimeOffset.TryParse(s, System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
            out var dto) ? dto : null;
    }

    private static TimeZoneInfo ResolveTimezone(string tzId)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(tzId); }
        catch
        {
            // Windows uses "W. Europe Standard Time" rather than IANA "Europe/Oslo".
            try { return TimeZoneInfo.FindSystemTimeZoneById("W. Europe Standard Time"); }
            catch { return TimeZoneInfo.Utc; }
        }
    }
}
