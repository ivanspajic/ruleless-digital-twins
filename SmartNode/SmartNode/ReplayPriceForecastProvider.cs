using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Logic.Mapek.Proactive;
using Microsoft.Extensions.Logging;

namespace SmartNode;

// WP3: deterministic, offline price provider. Reads a recorded JSON file of
// hourly slots from PRICE_REPLAY_FILE (or the default sample) and exposes them
// through the same IPriceForecastProvider contract used by the live Nord Pool
// adapter. The replay path never contacts Home Assistant and never reads
// TOKEN_HA, so demos and experiments remain reproducible without a live HA.
internal sealed class ReplayPriceForecastProvider : IPriceForecastProvider
{
    public const string ProviderName = "replay";
    public const string ReplayFileEnvVar = "PRICE_REPLAY_FILE";
    public const string DefaultReplayFile = "config/price-replay.sample.json";
    public const string ForecastSource = "replay";

    // Opt-in demo aid: when PRICE_REPLAY_REBASE_TODAY is truthy, the loaded slots
    // are shifted by a whole number of days so the sample's day covers "now",
    // making currentPriceNokPerKwh non-null at any date. Off by default — the
    // real recorded prices and existing tests keep their absolute timestamps,
    // and the live Nord Pool path is untouched.
    public const string RebaseEnvVar = "PRICE_REPLAY_REBASE_TODAY";

    private readonly ILogger<ReplayPriceForecastProvider> _logger;

    public ReplayPriceForecastProvider(ILogger<ReplayPriceForecastProvider> logger)
    {
        _logger = logger;
    }

    public static string GetConfiguredFile()
    {
        var raw = Environment.GetEnvironmentVariable(ReplayFileEnvVar);
        return string.IsNullOrWhiteSpace(raw) ? DefaultReplayFile : raw.Trim();
    }

    // Resolved (cwd-first then assembly-relative) form of GetConfiguredFile().
    // Callers that surface the replay file path to a user — e.g. /api/price
    // success and 503 envelopes — should use this so the value matches the
    // path the loader actually inspected.
    public static string GetResolvedFile()
        => ResolveReplayPath(GetConfiguredFile());

    public static bool IsRebaseEnabled()
    {
        var raw = Environment.GetEnvironmentVariable(RebaseEnvVar);
        if (string.IsNullOrWhiteSpace(raw)) return false;
        raw = raw.Trim();
        return raw.Equals("1", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("true", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    // Shift slots by a whole number of days so the earliest slot's day maps onto
    // the day containing `now`. Prices and ordering are preserved — only the
    // dates move. Because a full-day sample spans 24h, the result has a slot whose
    // [Start, End) interval contains `now`. Pure and deterministic given `now`;
    // `ordered` must be sorted ascending by Start.
    public static IReadOnlyList<PriceSlot> RebaseToNow(IReadOnlyList<PriceSlot> ordered, DateTimeOffset now)
    {
        if (ordered.Count == 0) return ordered;

        var first = ordered[0].Start;
        // Whole-day shift k such that first + k days <= now < first + (k+1) days.
        var shiftDays = (int)Math.Floor((now - first).TotalDays);
        if (shiftDays == 0) return ordered;

        var shift = TimeSpan.FromDays(shiftDays);
        return ordered
            .Select(s => new PriceSlot(s.Start + shift, s.End + shift, s.Price))
            .ToList();
    }

    public Task<PriceForecast> GetForecastAsync(CancellationToken ct = default)
    {
        var configured = GetConfiguredFile();
        var resolved = ResolveReplayPath(configured);
        var data = LoadAndValidate(resolved);

        _logger.LogInformation(
            "Replay price provider: loaded {n} slots from {path} (area={area} currency={currency} unit={unit})",
            data.Slots!.Count, resolved, data.Area ?? "<unset>", data.Currency ?? "<unset>", data.Unit ?? "<unset>");

        var slots = data.Slots!
            .Select(s => new PriceSlot(s.StartParsed, s.EndParsed, s.Price))
            .OrderBy(s => s.Start)
            .ToList();

        if (IsRebaseEnabled())
        {
            slots = RebaseToNow(slots, DateTimeOffset.UtcNow).ToList();
            _logger.LogInformation(
                "Replay price provider: {env} enabled — slots rebased by whole days to cover the current day.",
                RebaseEnvVar);
        }

        return Task.FromResult(new PriceForecast {
            Available = true,
            Source = string.IsNullOrWhiteSpace(data.Source) ? ForecastSource : data.Source!,
            Area = data.Area ?? string.Empty,
            Currency = data.Currency ?? string.Empty,
            Unit = data.Unit ?? string.Empty,
            Timezone = data.Timezone ?? string.Empty,
            Slots = slots
        });
    }

    // Distinct exception type so callers (e.g. /api/price) can produce a clean
    // 503 envelope instead of a generic 500 with a raw stack trace.
    public sealed class ReplayLoadException : Exception
    {
        public ReplayLoadException(string message) : base(message) { }
        public ReplayLoadException(string message, Exception inner) : base(message, inner) { }
    }

    public static ReplayData LoadAndValidate(string fullPath)
    {
        if (!File.Exists(fullPath))
        {
            throw new ReplayLoadException(
                $"Replay price file not found: '{fullPath}'. " +
                $"Set {ReplayFileEnvVar} to a valid JSON file or restore the sample at '{DefaultReplayFile}'.");
        }

        ReplayData? data;
        try
        {
            using var stream = File.OpenRead(fullPath);
            data = JsonSerializer.Deserialize<ReplayData>(stream, new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (JsonException ex)
        {
            throw new ReplayLoadException(
                $"Replay price file '{fullPath}' is not valid JSON: {ex.Message}", ex);
        }
        catch (IOException ex)
        {
            throw new ReplayLoadException(
                $"Replay price file '{fullPath}' could not be read: {ex.Message}", ex);
        }

        if (data is null)
        {
            throw new ReplayLoadException($"Replay price file '{fullPath}' is empty or null.");
        }

        if (data.Slots is null || data.Slots.Count == 0)
        {
            throw new ReplayLoadException(
                $"Replay price file '{fullPath}' contains no slots. Provide a non-empty 'slots' array.");
        }

        for (int i = 0; i < data.Slots.Count; i++)
        {
            var slot = data.Slots[i];
            if (string.IsNullOrWhiteSpace(slot.Start))
                throw new ReplayLoadException($"Replay slot #{i} in '{fullPath}' is missing 'start'.");
            if (string.IsNullOrWhiteSpace(slot.End))
                throw new ReplayLoadException($"Replay slot #{i} in '{fullPath}' is missing 'end'.");

            if (!DateTimeOffset.TryParse(slot.Start, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.RoundtripKind,
                out var startParsed))
            {
                throw new ReplayLoadException(
                    $"Replay slot #{i} in '{fullPath}' has invalid 'start' value '{slot.Start}'.");
            }
            if (!DateTimeOffset.TryParse(slot.End, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.RoundtripKind,
                out var endParsed))
            {
                throw new ReplayLoadException(
                    $"Replay slot #{i} in '{fullPath}' has invalid 'end' value '{slot.End}'.");
            }
            if (endParsed <= startParsed)
            {
                throw new ReplayLoadException(
                    $"Replay slot #{i} in '{fullPath}' has end <= start ({slot.End} <= {slot.Start}).");
            }

            slot.StartParsed = startParsed;
            slot.EndParsed = endParsed;
        }

        return data;
    }

    // Resolve `PRICE_REPLAY_FILE` exactly like the bindings loader does: cwd
    // first, then climb a few directories from the assembly so `dotnet run`
    // works from any working directory.
    public static string ResolveReplayPath(string configured)
    {
        if (Path.IsPathRooted(configured)) return configured;

        var fromCwd = Path.GetFullPath(configured);
        if (File.Exists(fromCwd)) return fromCwd;

        var asmDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        if (!string.IsNullOrEmpty(asmDir))
        {
            var probe = asmDir;
            for (int i = 0; i < 6 && !string.IsNullOrEmpty(probe); i++)
            {
                var candidate = Path.GetFullPath(Path.Combine(probe, configured));
                if (File.Exists(candidate)) return candidate;
                probe = Path.GetDirectoryName(probe);
            }
        }

        return fromCwd;
    }

    public sealed class ReplayData
    {
        [JsonPropertyName("source")]   public string? Source { get; set; }
        [JsonPropertyName("area")]     public string? Area { get; set; }
        [JsonPropertyName("currency")] public string? Currency { get; set; }
        [JsonPropertyName("unit")]     public string? Unit { get; set; }
        [JsonPropertyName("timezone")] public string? Timezone { get; set; }
        [JsonPropertyName("slots")]    public List<ReplaySlot>? Slots { get; set; }
    }

    public sealed class ReplaySlot
    {
        [JsonPropertyName("start")] public string? Start { get; set; }
        [JsonPropertyName("end")]   public string? End { get; set; }
        [JsonPropertyName("price")] public double Price { get; set; }

        [JsonIgnore] public DateTimeOffset StartParsed { get; set; }
        [JsonIgnore] public DateTimeOffset EndParsed { get; set; }
    }
}
