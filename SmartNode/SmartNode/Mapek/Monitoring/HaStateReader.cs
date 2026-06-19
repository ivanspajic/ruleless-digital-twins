using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace SmartNode.Mapek.Monitoring;

// Default IHaStateReader (issue #52). Mirrors what the existing /api/ha/states
// proxy does: GET HA's /api/states with a Bearer token from TOKEN_HA, base
// URL from HA_URL. Returns a flat entity_id => state-string snapshot, or null
// on any failure. Errors are swallowed by design so the MAPE-K dry-run tick
// can keep returning a useful response (with an "unavailable" warning) when
// HA is offline. No actuation, no service calls — read-only.
public sealed class HaStateReader : IHaStateReader
{
    private const string DefaultBaseUrl = "http://localhost:8123/";
    private const string TokenEnvVar = "TOKEN_HA";
    private const string UrlEnvVar = "HA_URL";

    private readonly string _baseUrl;
    private readonly string _token;
    private readonly TimeSpan _timeout;
    private readonly ILogger<HaStateReader>? _logger;

    public HaStateReader(ILogger<HaStateReader>? logger = null)
        : this(
            ResolveBaseUrl(Environment.GetEnvironmentVariable(UrlEnvVar)),
            Environment.GetEnvironmentVariable(TokenEnvVar) ?? string.Empty,
            TimeSpan.FromSeconds(5),
            logger)
    { }

    internal HaStateReader(string baseUrl, string token, TimeSpan timeout, ILogger<HaStateReader>? logger = null)
    {
        _baseUrl = baseUrl;
        _token = token;
        _timeout = timeout;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, object?>?> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_token))
        {
            _logger?.LogDebug("HaStateReader: TOKEN_HA empty — skipping HA snapshot read.");
            return null;
        }

        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(_baseUrl), Timeout = _timeout };
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);
            var raw = await http.GetStringAsync("api/states", cancellationToken);
            return ParseSnapshot(raw);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug("HaStateReader: read failed ({msg}); reporting snapshot as unavailable.", ex.Message);
            return null;
        }
    }

    internal static IReadOnlyDictionary<string, object?>? ParseSnapshot(string raw)
    {
        try
        {
            using var doc = JsonDocument.Parse(raw);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) return null;

            var snapshot = new Dictionary<string, object?>(doc.RootElement.GetArrayLength());
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                if (!item.TryGetProperty("entity_id", out var idEl) || idEl.ValueKind != JsonValueKind.String) continue;
                var id = idEl.GetString()!;

                string? state = null;
                if (item.TryGetProperty("state", out var stEl) && stEl.ValueKind == JsonValueKind.String)
                {
                    state = stEl.GetString();
                }
                snapshot[id] = state;
            }
            return snapshot;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string ResolveBaseUrl(string? raw)
    {
        var url = string.IsNullOrWhiteSpace(raw) ? DefaultBaseUrl : raw;
        if (!url.EndsWith("/")) url += "/";
        return url;
    }
}
