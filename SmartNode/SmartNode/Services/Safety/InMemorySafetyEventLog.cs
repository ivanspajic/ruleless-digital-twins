using SmartNode.Mapek.Execution;

namespace SmartNode.Services.Safety;

// Thread-safe, in-memory bounded audit log (default provider). Keeps the last
// `capacity` events and never touches disk; the log resets on process restart.
// Acceptable while real execution is manual/endpoint-driven; set
// MAPEK_SAFETY_EVENT_LOG_PROVIDER=sqlite for a durable audit trail.
public sealed class InMemorySafetyEventLog : ISafetyEventLog
{
    private readonly int _capacity;
    private readonly object _gate = new();
    private readonly LinkedList<SafetyEventRecord> _events = new();

    public InMemorySafetyEventLog(int capacity = 500)
    {
        _capacity = capacity > 0 ? capacity : 500;
    }

    public void Record(SafetyEventRecord safetyEvent)
    {
        ArgumentNullException.ThrowIfNull(safetyEvent);
        lock (_gate)
        {
            _events.AddLast(safetyEvent);
            while (_events.Count > _capacity)
            {
                _events.RemoveFirst();
            }
        }
    }

    public IReadOnlyList<SafetyEventRecord> GetRecent()
    {
        lock (_gate)
        {
            return _events.ToList();
        }
    }
}
