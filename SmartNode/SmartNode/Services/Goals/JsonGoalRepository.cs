using Microsoft.Extensions.Logging;
using SmartNode.Models.Goals;

namespace SmartNode.Services.Goals;

// JSON-file goal provider (the default). Re-reads the file on
// every GetAll() so manual edits are picked up between ticks — preserving the
// previous per-tick load behaviour. Parsing and credential-marker filtering are
// delegated to GoalRepository. A missing/unresolvable file yields an empty list,
// exactly as before.
public sealed class JsonGoalRepository : IGoalRepository
{
    private readonly string _configuredPath;
    private readonly ILogger<GoalRepository>? _logger;

    public JsonGoalRepository(string configuredPath, ILogger<GoalRepository>? logger = null)
    {
        _configuredPath = configuredPath;
        _logger = logger;
    }

    public IReadOnlyList<UserGoal> GetAll()
    {
        var repo = new GoalRepository(_logger);
        var resolved = Resolve(_configuredPath);
        if (resolved is not null)
        {
            repo.LoadFromFile(resolved);
        }
        return repo.GetAll();
    }

    // Resolve the configured path cwd-first, then climbing from the assembly
    // directory, so `dotnet run` works from any working directory (same idea as
    // the bindings/replay resolvers). Returns null when nothing matches.
    public static string? Resolve(string configured)
    {
        if (string.IsNullOrWhiteSpace(configured)) return null;

        if (Path.IsPathRooted(configured))
        {
            return File.Exists(configured) ? configured : null;
        }

        var fromCwd = Path.GetFullPath(configured);
        if (File.Exists(fromCwd)) return fromCwd;

        var probe = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
        for (var i = 0; i < 6 && !string.IsNullOrEmpty(probe); i++)
        {
            var candidate = Path.GetFullPath(Path.Combine(probe!, configured));
            if (File.Exists(candidate)) return candidate;
            probe = Path.GetDirectoryName(probe);
        }

        return null;
    }
}
