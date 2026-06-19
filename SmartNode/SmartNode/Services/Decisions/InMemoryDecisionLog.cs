using SmartNode.Models.MapeK;

namespace SmartNode.Services.Decisions;

// Thread-safe, in-memory rolling decision log (step 1.7). Keeps only the last
// `Capacity` decisions and never touches disk, so it carries no risk of
// committing runtime logs or secrets. Decisions live for the process lifetime —
// enough to inspect during a demo or scrape via /api/mapek/decisions; durable
// file persistence is a deliberate later step.
public sealed class InMemoryDecisionLog : IDecisionLog
{
    private const int Capacity = 50;

    private readonly object _gate = new();
    private readonly LinkedList<DecisionLogEntry> _entries = new();

    public void Append(DecisionLogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
        {
            _entries.AddLast(entry);
            while (_entries.Count > Capacity)
            {
                _entries.RemoveFirst();
            }
        }
    }

    public IReadOnlyList<DecisionLogEntry> GetRecent(int max = 20)
    {
        if (max <= 0) return Array.Empty<DecisionLogEntry>();
        lock (_gate)
        {
            // Newest first.
            var result = new List<DecisionLogEntry>(Math.Min(max, _entries.Count));
            for (var node = _entries.Last; node is not null && result.Count < max; node = node.Previous)
            {
                result.Add(node.Value);
            }
            return result;
        }
    }
}
