using System.Text.Json;
using System.Text.RegularExpressions;
using SmartNode.Models.Goals;

namespace SmartNode.Services.Goals;

// Result of a goal-editor operation: an HTTP status code and a JSON-serialisable
// payload. The HTTP layer (Program.cs) serialises the payload (camelCase) and
// sets the status — keeping this orchestration pure and offline-testable.
internal sealed record GoalEditorResult(int StatusCode, object? Payload);

// Pure orchestration for the goal-editor CRUD endpoints.
// No HTTP, no DI, no I/O of its own — it operates on an IGoalRepository so it can
// be unit-tested offline. Writes require an IMutableGoalRepository; otherwise they
// fail with 409 (the method is valid under sqlite, but incompatible with the
// current read-only provider).
internal static class GoalEditorEndpoint
{
    internal const string ReadOnlyMessage =
        "Goal writes require a mutable provider. Set GOAL_REPOSITORY_PROVIDER=sqlite; " +
        "the current provider is read-only.";

    private static readonly JsonSerializerOptions ParseOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly Regex IdentifierPattern = new("^[A-Za-z0-9_]+$", RegexOptions.Compiled);
    private static readonly Regex EntityIdPattern = new("^[A-Za-z0-9_]+\\.[A-Za-z0-9_]+$", RegexOptions.Compiled);

    public static GoalEditorResult List(IGoalRepository repo)
    {
        var goals = repo.GetAll();
        return new GoalEditorResult(200, new { count = goals.Count, goals });
    }

    public static GoalEditorResult GetById(IGoalRepository repo, string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return new GoalEditorResult(400, new { error = "Goal id is required." });

        var goal = repo.GetAll().FirstOrDefault(g => string.Equals(g.Id, id, StringComparison.Ordinal));
        return goal is null
            ? new GoalEditorResult(404, new { error = $"Goal '{id}' not found." })
            : new GoalEditorResult(200, goal);
    }

    public static GoalEditorResult Create(IGoalRepository repo, string? rawBody)
    {
        if (repo is not IMutableGoalRepository mutable)
            return new GoalEditorResult(409, new { error = ReadOnlyMessage });

        if (string.IsNullOrWhiteSpace(rawBody))
            return new GoalEditorResult(400, new { error = "Request body is required." });

        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            root = doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            return new GoalEditorResult(400, new { error = $"Invalid JSON: {ex.Message}" });
        }

        // Reject any goal carrying credential-like markers, consistent with the
        // JSON loader and the "no secrets in goals" rule.
        if (GoalRepository.ContainsCredentialMarker(root))
        {
            return new GoalEditorResult(400, new
            {
                error = "Goal must not contain credential-like markers (bearer/token/secret/password/apikey)."
            });
        }

        UserGoal? goal;
        try
        {
            goal = root.Deserialize<UserGoal>(ParseOptions);
        }
        catch (JsonException ex)
        {
            return new GoalEditorResult(400, new { error = $"Invalid goal: {ex.Message}" });
        }

        var validation = ValidateGoal(goal);
        if (validation is not null)
            return new GoalEditorResult(400, new { error = validation });

        mutable.Upsert(goal!);
        return new GoalEditorResult(200, goal);
    }

    public static GoalEditorResult Replace(IGoalRepository repo, string id, string? rawBody)
    {
        if (repo is not IMutableGoalRepository mutable)
            return new GoalEditorResult(409, new { error = ReadOnlyMessage });

        if (string.IsNullOrWhiteSpace(id))
            return new GoalEditorResult(400, new { error = "Goal id is required." });

        if (string.IsNullOrWhiteSpace(rawBody))
            return new GoalEditorResult(400, new { error = "Request body is required." });

        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            root = doc.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            return new GoalEditorResult(400, new { error = $"Invalid JSON: {ex.Message}" });
        }

        if (GoalRepository.ContainsCredentialMarker(root))
        {
            return new GoalEditorResult(400, new
            {
                error = "Goal must not contain credential-like markers (bearer/token/secret/password/apikey)."
            });
        }

        UserGoal? goal;
        try
        {
            goal = root.Deserialize<UserGoal>(ParseOptions);
        }
        catch (JsonException ex)
        {
            return new GoalEditorResult(400, new { error = $"Invalid goal: {ex.Message}" });
        }

        var validation = ValidateGoal(goal);
        if (validation is not null)
            return new GoalEditorResult(400, new { error = validation });

        if (!string.Equals(goal!.Id, id, StringComparison.Ordinal))
            return new GoalEditorResult(400, new { error = "Goal id in the body must match the URL id." });

        if (mutable.GetById(id) is null)
            return new GoalEditorResult(404, new { error = $"Goal '{id}' not found." });

        mutable.Upsert(goal);
        return new GoalEditorResult(200, goal);
    }

    public static GoalEditorResult Delete(IGoalRepository repo, string id)
    {
        if (repo is not IMutableGoalRepository mutable)
            return new GoalEditorResult(409, new { error = ReadOnlyMessage });

        if (string.IsNullOrWhiteSpace(id))
            return new GoalEditorResult(400, new { error = "Goal id is required." });

        return mutable.Delete(id)
            ? new GoalEditorResult(200, new { deleted = true, id })
            : new GoalEditorResult(404, new { error = $"Goal '{id}' not found." });
    }

    // Enable/disable a goal without resending its full definition (criterion 6 —
    // goals can be disabled from a UI toggle, no manual JSON). Body is a minimal
    // JSON object: {"enabled": true|false}. Only the Enabled flag changes; all
    // other goal fields are preserved.
    public static GoalEditorResult SetEnabled(IGoalRepository repo, string id, string? rawBody)
    {
        if (repo is not IMutableGoalRepository mutable)
            return new GoalEditorResult(409, new { error = ReadOnlyMessage });

        if (string.IsNullOrWhiteSpace(id))
            return new GoalEditorResult(400, new { error = "Goal id is required." });

        var enabled = ParseEnabledFlag(rawBody);
        if (enabled is null)
            return new GoalEditorResult(400, new
            {
                error = "Request body must be a JSON object with a boolean 'enabled' field."
            });

        var goal = mutable.GetById(id);
        if (goal is null)
            return new GoalEditorResult(404, new { error = $"Goal '{id}' not found." });

        var updated = goal with { Enabled = enabled.Value };
        mutable.Upsert(updated);
        return new GoalEditorResult(200, updated);
    }

    // Returns the boolean 'enabled' field, or null if the body is missing,
    // malformed, lacks the field, or it is not a JSON boolean.
    private static bool? ParseEnabledFlag(string? rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody)) return null;
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("enabled", out var flag))
            {
                if (flag.ValueKind == JsonValueKind.True) return true;
                if (flag.ValueKind == JsonValueKind.False) return false;
            }
        }
        catch (JsonException)
        {
            // fall through to null
        }
        return null;
    }

    private static string? ValidateGoal(UserGoal? goal)
    {
        if (goal is null) return "Goal payload is required.";
        if (string.IsNullOrWhiteSpace(goal.Id)) return "Goal 'id' is required.";
        if (string.IsNullOrWhiteSpace(goal.Type)) return "Goal 'type' is required.";
        if (goal.Condition is null) return "Goal 'condition' is required.";
        if (goal.Objective is null) return "Goal 'objective' is required.";
        if (goal.Constraints is null) return "Goal 'constraints' is required.";
        if (goal.Actions is null) return "Goal 'actions' is required.";

        foreach (var action in goal.Actions)
        {
            if (action is null) return "Goal action must not be null.";
            if (string.IsNullOrWhiteSpace(action.Domain) || !IdentifierPattern.IsMatch(action.Domain))
                return "Goal action 'domain' is required and must use letters, numbers or underscore.";
            if (string.IsNullOrWhiteSpace(action.Service) || !IdentifierPattern.IsMatch(action.Service))
                return "Goal action 'service' is required and must use letters, numbers or underscore.";
            if (string.IsNullOrWhiteSpace(action.EntityId) || !EntityIdPattern.IsMatch(action.EntityId))
                return "Goal action 'entityId' is required and must be a Home Assistant entity id like domain.object.";
            if (!string.Equals(action.Domain, action.EntityId.Split('.')[0], StringComparison.OrdinalIgnoreCase))
                return "Goal action domain must match the entity id domain.";
        }

        return null;
    }
}
