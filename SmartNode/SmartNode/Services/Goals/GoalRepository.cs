using System.Text.Json;
using Microsoft.Extensions.Logging;
using SmartNode.Models.Goals;

namespace SmartNode.Services.Goals;

// In-memory user-goal store. Goals reset on every restart — no persistence layer.
// Loads goals from a JSON file on demand and rejects any entry containing
// obvious credential markers, so the goals file is never used as a secrets channel.
public sealed class GoalRepository : IGoalRepository
{
    private static readonly string[] CredentialMarkers =
    {
        "bearer ", "token", "secret", "password", "credential", "apikey"
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ILogger<GoalRepository>? _logger;
    private List<UserGoal> _goals = new();

    public GoalRepository(ILogger<GoalRepository>? logger = null)
    {
        _logger = logger;
    }

    public IReadOnlyList<UserGoal> GetAll() => _goals;

    public void LoadFromFile(string path)
    {
        if (!File.Exists(path))
        {
            _logger?.LogWarning(
                "GoalRepository: file '{Path}' not found; starting with empty goal list.", path);
            _goals = new List<UserGoal>();
            return;
        }

        var json = File.ReadAllText(path);
        using var doc = JsonDocument.Parse(json);

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
        {
            _logger?.LogWarning(
                "GoalRepository: '{Path}' root must be a JSON array; ignoring file.", path);
            _goals = new List<UserGoal>();
            return;
        }

        var loaded = new List<UserGoal>();
        var skipped = 0;

        foreach (var element in doc.RootElement.EnumerateArray())
        {
            if (ContainsCredentialMarker(element))
            {
                skipped++;
                _logger?.LogWarning(
                    "GoalRepository: skipped a goal containing a credential-like marker. " +
                    "User goals must not embed bearer tokens, secrets, passwords, or API keys.");
                continue;
            }

            try
            {
                var goal = element.Deserialize<UserGoal>(JsonOptions);
                if (goal is not null) loaded.Add(goal);
            }
            catch (JsonException ex)
            {
                skipped++;
                _logger?.LogWarning(ex, "GoalRepository: skipped a malformed goal entry.");
            }
        }

        _goals = loaded;

        if (skipped > 0)
        {
            _logger?.LogWarning(
                "GoalRepository: loaded {Loaded} goals, skipped {Skipped} unsafe or invalid entries.",
                loaded.Count, skipped);
        }
    }

    // Recursively checks a JSON value for credential-like markers (bearer, token,
    // secret, password, credential, apikey). Public so the goal-editor endpoint
    // can reject a POSTed goal that embeds secrets.
    public static bool ContainsCredentialMarker(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    if (MatchesMarker(prop.Name)) return true;
                    if (ContainsCredentialMarker(prop.Value)) return true;
                }
                return false;

            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    if (ContainsCredentialMarker(item)) return true;
                }
                return false;

            case JsonValueKind.String:
                return MatchesMarker(element.GetString() ?? string.Empty);

            default:
                return false;
        }
    }

    private static bool MatchesMarker(string value)
    {
        var lower = value.ToLowerInvariant();
        foreach (var marker in CredentialMarkers)
        {
            if (lower.Contains(marker)) return true;
        }
        return false;
    }
}
