namespace SmartNode.Services.Bindings;

// One row in the bindings-adoption history (PR A3 infrastructure). Stores ONLY
// hashes, the validation summary, metadata and paths — NEVER the binding config
// JSON and NEVER a Home Assistant token. There is intentionally no "config"
// field: the history records *that* an adoption happened and its fingerprints,
// not the adopted document itself.
public sealed record BindingsRevisionRecord(
    string RevisionId,
    DateTimeOffset CreatedAtUtc,
    string? Profile,
    string Status,              // see BindingsRevisionStatus
    string CurrentHash,         // hash of the runtime config before this revision
    string AdoptedHash,         // hash of the config this revision concerns
    string ValidationStatus,    // PASS | WARN | FAIL
    int ErrorCount,
    int WarningCount,
    bool DryRun,
    string? BackupPath = null,
    string? TargetPath = null,
    string? Reason = null,
    string? CreatedBy = null,
    string? MetadataJson = null);

// Lifecycle states a revision can carry. A4 sets `Adopted`/`Failed`, A6 sets
// `RolledBack`; `Preview` is reserved for a possible future dry-run record (not
// written by the dry-run endpoint in this slice).
public static class BindingsRevisionStatus
{
    public const string Preview = "preview";
    public const string Adopted = "adopted";
    public const string Failed = "failed";
    public const string RolledBack = "rolled_back";
}
