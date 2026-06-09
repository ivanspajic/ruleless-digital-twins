using System.Globalization;
using Microsoft.Data.Sqlite;

namespace SmartNode.Services.Bindings;

// Durable, SQLite-backed bindings revision history. Persists every adoption
// revision so the history survives a SmartNode restart. Thread-safe via a single
// guarded connection. Stores NO secrets and NO config JSON — only hashes, the
// validation summary, metadata and paths. Fail-fast: the constructor throws a
// clear error if the database cannot be created/opened. Mirrors the existing
// SqliteSafetyEventLog pattern.
public sealed class SqliteBindingsRevisionStore : IBindingsRevisionStore, IDisposable
{
    private readonly object _gate = new();
    private readonly SqliteConnection _connection;
    private bool _disposed;

    private const string SelectColumns = @"
SELECT revision_id, created_at_utc, profile, status, current_hash, adopted_hash,
       validation_status, error_count, warning_count, dry_run,
       backup_path, target_path, reason, created_by, metadata_json
FROM bindings_revisions";

    public SqliteBindingsRevisionStore(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Bindings revision store SQLite path must not be empty.", nameof(databasePath));
        }

        var fullPath = Path.GetFullPath(databasePath);
        try
        {
            var dir = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var connectionString = new SqliteConnectionStringBuilder { DataSource = fullPath }.ToString();
            _connection = new SqliteConnection(connectionString);
            _connection.Open();

            Execute("PRAGMA journal_mode=WAL; PRAGMA busy_timeout=5000;");
            Execute(@"
CREATE TABLE IF NOT EXISTS bindings_revisions (
    seq               INTEGER PRIMARY KEY AUTOINCREMENT,
    revision_id       TEXT    NOT NULL UNIQUE,
    created_at_utc    TEXT    NOT NULL,
    profile           TEXT    NULL,
    status            TEXT    NOT NULL,
    current_hash      TEXT    NOT NULL,
    adopted_hash      TEXT    NOT NULL,
    validation_status TEXT    NOT NULL,
    error_count       INTEGER NOT NULL,
    warning_count     INTEGER NOT NULL,
    dry_run           INTEGER NOT NULL,
    backup_path       TEXT    NULL,
    target_path       TEXT    NULL,
    reason            TEXT    NULL,
    created_by        TEXT    NULL,
    metadata_json     TEXT    NULL
);");
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new InvalidOperationException(
                $"Failed to initialise the SQLite bindings revision store at '{fullPath}'. " +
                $"Check {BindingsRevisionStoreOptions.SqlitePathEnvVar} and filesystem permissions. ({ex.Message})", ex);
        }
    }

    public void Append(BindingsRevisionRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        lock (_gate)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
INSERT INTO bindings_revisions
 (revision_id, created_at_utc, profile, status, current_hash, adopted_hash,
  validation_status, error_count, warning_count, dry_run,
  backup_path, target_path, reason, created_by, metadata_json)
VALUES ($rid, $ts, $profile, $status, $cur, $adopt,
        $vstatus, $err, $warn, $dry,
        $backup, $target, $reason, $by, $meta);";
            cmd.Parameters.AddWithValue("$rid", record.RevisionId);
            cmd.Parameters.AddWithValue("$ts", record.CreatedAtUtc.ToString("o", CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("$profile", (object?)record.Profile ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$status", record.Status);
            cmd.Parameters.AddWithValue("$cur", record.CurrentHash);
            cmd.Parameters.AddWithValue("$adopt", record.AdoptedHash);
            cmd.Parameters.AddWithValue("$vstatus", record.ValidationStatus);
            cmd.Parameters.AddWithValue("$err", record.ErrorCount);
            cmd.Parameters.AddWithValue("$warn", record.WarningCount);
            cmd.Parameters.AddWithValue("$dry", record.DryRun ? 1 : 0);
            cmd.Parameters.AddWithValue("$backup", (object?)record.BackupPath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$target", (object?)record.TargetPath ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$reason", (object?)record.Reason ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$by", (object?)record.CreatedBy ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$meta", (object?)record.MetadataJson ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
    }

    public IReadOnlyList<BindingsRevisionRecord> GetRecent(int limit)
    {
        if (limit <= 0) return Array.Empty<BindingsRevisionRecord>();
        lock (_gate)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = SelectColumns + "\nORDER BY seq DESC\nLIMIT $limit;";
            cmd.Parameters.AddWithValue("$limit", limit);
            return ReadAll(cmd);
        }
    }

    public BindingsRevisionRecord? GetById(string revisionId)
    {
        if (string.IsNullOrWhiteSpace(revisionId)) return null;
        lock (_gate)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = SelectColumns + "\nWHERE revision_id = $rid\nLIMIT 1;";
            cmd.Parameters.AddWithValue("$rid", revisionId);
            var rows = ReadAll(cmd);
            return rows.Count > 0 ? rows[0] : null;
        }
    }

    private static List<BindingsRevisionRecord> ReadAll(SqliteCommand cmd)
    {
        var result = new List<BindingsRevisionRecord>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new BindingsRevisionRecord(
                RevisionId: reader.GetString(0),
                CreatedAtUtc: DateTimeOffset.Parse(
                    reader.GetString(1), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind),
                Profile: reader.IsDBNull(2) ? null : reader.GetString(2),
                Status: reader.GetString(3),
                CurrentHash: reader.GetString(4),
                AdoptedHash: reader.GetString(5),
                ValidationStatus: reader.GetString(6),
                ErrorCount: reader.GetInt32(7),
                WarningCount: reader.GetInt32(8),
                DryRun: reader.GetInt32(9) != 0,
                BackupPath: reader.IsDBNull(10) ? null : reader.GetString(10),
                TargetPath: reader.IsDBNull(11) ? null : reader.GetString(11),
                Reason: reader.IsDBNull(12) ? null : reader.GetString(12),
                CreatedBy: reader.IsDBNull(13) ? null : reader.GetString(13),
                MetadataJson: reader.IsDBNull(14) ? null : reader.GetString(14)));
        }
        return result;
    }

    private void Execute(string sql)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _connection.Dispose();
    }
}
