using SmartNode.Mapek.Execution;

namespace SmartNode.Services.Safety;

// Append-only audit log of real-execution safety decisions (P1 / criterion 10).
// The tick records one event per blocked attempt, failure, or successful
// actuation; an operator (or a future /safety endpoint) reads recent events back
// to see *why* the system did or did not act. Recording is best-effort at the
// call site: an audit-store failure must never change safety control flow.
public interface ISafetyEventLog
{
    void Record(SafetyEventRecord safetyEvent);

    // Most recent events (bounded). Order is not part of the contract; readers
    // that need chronology should sort by Timestamp.
    IReadOnlyList<SafetyEventRecord> GetRecent();
}
