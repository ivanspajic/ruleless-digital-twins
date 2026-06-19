namespace SmartNode.Services.Bindings;

// Append-only history of binding-config adoptions (PR A3 infrastructure).
//
// The real adopt flow (A4) records one revision per adoption; rollback (A6)
// appends a `rolled_back` revision. This store keeps NO secrets — only hashes,
// the validation summary, metadata and paths (see BindingsRevisionRecord).
//
// Synchronous on purpose: it mirrors the existing store layer (ISafetyEventLog,
// IExecutionHistory) which is all synchronous. NOT yet wired into the dry-run
// adopt endpoint — this PR adds the infrastructure only.
public interface IBindingsRevisionStore
{
    void Append(BindingsRevisionRecord record);

    // Most recent revisions, newest first, bounded by `limit` (<= 0 → empty).
    IReadOnlyList<BindingsRevisionRecord> GetRecent(int limit);

    // The revision with this id, or null when unknown.
    BindingsRevisionRecord? GetById(string revisionId);
}
