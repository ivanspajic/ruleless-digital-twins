using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SmartNode
{
    public class HomeAssistantEntity
    {
        public string EntityId { get; set; } = string.Empty;
        public string Domain => EntityId.Split('.')[0];
        public string FriendlyName { get; set; } = string.Empty;
        public string State { get; set; } = string.Empty;
    }

    public class HomeAssistantRegistry
    {
        private readonly HttpClient _http;
        private readonly ILogger _logger;
        private readonly string _token;
        private List<HomeAssistantEntity> _entities = new();
        private Timer? _timer;

        public HomeAssistantRegistry(ILogger<HomeAssistantRegistry> logger)
        {
            _logger = logger;
            _token = Environment.GetEnvironmentVariable("TOKEN_HA") ?? string.Empty;
            var haUrl = Environment.GetEnvironmentVariable("HA_URL");
            if (string.IsNullOrWhiteSpace(haUrl)) haUrl = "http://localhost:8123/";
            if (!haUrl.EndsWith("/")) haUrl += "/";
            _http = new HttpClient { BaseAddress = new Uri(haUrl) };
            _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _token);
        }

        // Pure decision, exposed for unit testing: the background refresh only
        // makes sense with a token, since every /api/states call is rejected with
        // 401 without one.
        public static bool ShouldRefresh(string? token) => !string.IsNullOrWhiteSpace(token);

        public void Start()
        {
            // Without a token every /api/states call is rejected with 401, so a
            // chatbox-only / no-token boot would log a spurious warning on each
            // cycle. Skip the refresh entirely to keep that start strictly offline
            // on the Home Assistant side. When TOKEN_HA is present, refresh runs as
            // before so genuine connectivity/auth problems still surface.
            if (!ShouldRefresh(_token))
            {
                _logger.LogInformation("Home Assistant registry refresh skipped: TOKEN_HA is not set.");
                return;
            }
            _timer = new Timer(async _ => await RefreshEntities(), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        }

        private async Task RefreshEntities()
        {
            try
            {
                var response = await _http.GetStringAsync("api/states");
                using var doc = JsonDocument.Parse(response);
                var newEntities = new List<HomeAssistantEntity>();
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var entityId = el.GetProperty("entity_id").GetString() ?? "";
                    var state = el.GetProperty("state").GetString() ?? "";
                    var friendlyName = entityId;
                    if (el.TryGetProperty("attributes", out var attr) && attr.TryGetProperty("friendly_name", out var fn))
                    {
                        friendlyName = fn.GetString() ?? entityId;
                    }
                    newEntities.Add(new HomeAssistantEntity { EntityId = entityId, FriendlyName = friendlyName, State = state });
                }
                _entities = newEntities;
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to refresh HA entities: {ex.Message}");
            }
        }

        public IReadOnlyList<HomeAssistantEntity> GetAll() => _entities;

        public string SummaryForPrompt()
        {
            var entitiesByDomain = _entities
                .GroupBy(e => e.Domain)
                .ToDictionary(g => g.Key, g => g.ToList());
                
            var lines = new List<string>();
            foreach (var kvp in entitiesByDomain)
            {
                var formattedEntities = kvp.Value.Select(e => $"{e.EntityId.Replace(kvp.Key + ".", "")} '{e.FriendlyName}'");
                lines.Add($"{kvp.Key}: {string.Join(", ", formattedEntities)}");
            }
            return string.Join("\n", lines);
        }
    }
}
