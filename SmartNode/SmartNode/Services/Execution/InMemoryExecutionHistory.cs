using SmartNode.Mapek.Execution;

namespace SmartNode.Services.Execution;

// Thread-safe, in-memory bounded store of recent real executions (P7-B). Keeps
// the last `capacity` records and never touches disk. History resets on process
// restart — a deliberate limitation for this phase: it is acceptable while real
// execution is manual/endpoint-driven, but a persistent store is required before
// autonomous real execution (P7-C) so a restart cannot reset cooldowns.
public sealed class InMemoryExecutionHistory : IExecutionHistory
{
    private readonly int _capacity;
    private readonly object _gate = new();
    private readonly LinkedList<ActionExecutionRecord> _records = new();

    public InMemoryExecutionHistory(int capacity = 500)
    {
        _capacity = capacity > 0 ? capacity : 500;
    }

    public void Record(ActionExecutionRecord record)
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

    public IReadOnlyList<ActionExecutionRecord> GetRecent()
    {
        lock (_gate)
        {
            return _records.ToList();
        }
    }
}
