namespace SmartNode.Services.Bindings;

// Thread-safe, in-memory bounded revision history (default provider). Keeps the
// last `capacity` revisions and never touches disk; resets on process restart.
// Set BINDINGS_REVISION_STORE_PROVIDER=sqlite for a durable history.
public sealed class InMemoryBindingsRevisionStore : IBindingsRevisionStore
{
    private readonly int _capacity;
    private readonly object _gate = new();
    private readonly LinkedList<BindingsRevisionRecord> _records = new(); // last = newest

    public InMemoryBindingsRevisionStore(int capacity = 500)
    {
        _capacity = capacity > 0 ? capacity : 500;
    }

    public void Append(BindingsRevisionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        lock (_gate)
        {
            _records.AddLast(record);
            while (_records.Count > _capacity)
            {
                _records.RemoveFirst();
            }
        }
    }

    public IReadOnlyList<BindingsRevisionRecord> GetRecent(int limit)
    {
        if (limit <= 0) return Array.Empty<BindingsRevisionRecord>();
        lock (_gate)
        {
            return _records.Reverse().Take(limit).ToList(); // newest first
        }
    }

    public BindingsRevisionRecord? GetById(string revisionId)
    {
        if (string.IsNullOrWhiteSpace(revisionId)) return null;
        lock (_gate)
        {
            return _records.LastOrDefault(r => r.RevisionId == revisionId); // newest match
        }
    }
}
