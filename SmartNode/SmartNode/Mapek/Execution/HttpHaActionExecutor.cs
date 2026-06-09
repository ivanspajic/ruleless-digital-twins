using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartNode.Models.MapeK;

namespace SmartNode.Mapek.Execution;

// Real executor: POST api/services/{domain}/{service} with a Bearer TOKEN_HA,
// mirroring the existing /api/call_service handler. The action's entityId is
// merged into the service payload as `entity_id` so the call targets the right
// device. Never throws — network/HA errors come back as
// HaExecutionResult(Executed: false, Error: ...).
public sealed class HttpHaActionExecutor : IHaActionExecutor
{
    // Targeting selectors are NOT accepted from the goal's `data`: the allowlist
    // authorizes a specific entity_id, so any of these in `data` is stripped and
    // the call is forced onto the validated EntityId. This prevents a goal from
    // broadening the blast radius (e.g. area_id="whole_house") past the allowlist.
    private static readonly HashSet<string> TargetingKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "entity_id", "area_id", "device_id", "label_id"
    };

    private readonly string _haUrl;
    private readonly string _token;
    private readonly ILogger<HttpHaActionExecutor>? _logger;

    public HttpHaActionExecutor(string haUrl, string token, ILogger<HttpHaActionExecutor>? logger = null)
    {
        _haUrl = haUrl;
        _token = token;
        _logger = logger;
    }

    public async Task<HaExecutionResult> ExecuteAsync(HaAction action, CancellationToken cancellationToken = default)
    {
        try
        {
            using var http = new HttpClient { BaseAddress = new Uri(_haUrl), Timeout = TimeSpan.FromSeconds(5) };
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _token);

            var body = JsonSerializer.Serialize(BuildPayload(action));
            using var content = new StringContent(body, Encoding.UTF8, "application/json");

            var res = await http.PostAsync(
                $"api/services/{action.Domain}/{action.Service}", content, cancellationToken);
            var ok = res.IsSuccessStatusCode;
            if (!ok)
            {
                _logger?.LogWarning("HA execution {domain}.{service} on {entity} -> HTTP {code}",
                    action.Domain, action.Service, action.EntityId, (int)res.StatusCode);
            }
            return new HaExecutionResult(
                ok, (int)res.StatusCode, ok ? null : $"Home Assistant returned HTTP {(int)res.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger?.LogWarning("HA execution {domain}.{service} on {entity} failed: {msg}",
                action.Domain, action.Service, action.EntityId, ex.Message);
            return new HaExecutionResult(false, null, ex.Message);
        }
    }

    // Build the HA service payload: keep only non-targeting service parameters
    // from `data`, then force `entity_id` to the validated EntityId. Pure and
    // side-effect free so the targeting policy is unit-testable without HTTP.
    public static IReadOnlyDictionary<string, object?> BuildPayload(HaAction action)
    {
        var payload = new Dictionary<string, object?>();
        if (action.Data is not null)
        {
            foreach (var kvp in action.Data)
            {
                if (TargetingKeys.Contains(kvp.Key)) continue;
                payload[kvp.Key] = kvp.Value;
            }
        }
        payload["entity_id"] = action.EntityId;
        return payload;
    }
}
