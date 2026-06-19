using Microsoft.Data.Sqlite;
using SmartNode.Services.Persistence;

namespace SmartNode.Services.Settings;

// Durable, SQLite-backed non-secret settings store. It intentionally stores only
// key/value product settings and rejects credential-like markers, so TOKEN_HA and
// other secrets stay in env/runtime-only paths.
public sealed class SqliteSettingsStore : ISettingsStore, IDisposable
{
    private readonly object _gate = new();
    private readonly SqliteConnection _connection;
    private bool _disposed;

    public SqliteSettingsStore(string databasePath)
    {
        if (string.IsNullOrWhiteSpace(databasePath))
        {
            throw new ArgumentException("Settings SQLite path must not be empty.", nameof(databasePath));
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
CREATE TABLE IF NOT EXISTS product_settings (
    key        TEXT PRIMARY KEY,
    value      TEXT NOT NULL,
    created_at TEXT NOT NULL,
    updated_at TEXT NOT NULL
);");
            MigrateLegacySettingsTable();
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            throw new InvalidOperationException(
                $"Failed to initialise the SQLite settings store at '{fullPath}'. " +
                $"Check {SettingsStoreOptions.SqlitePathEnvVar} and filesystem permissions. ({ex.Message})", ex);
        }
    }

    public IReadOnlyList<SettingEntry> GetAll()
    {
        lock (_gate)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT key, value, updated_at FROM product_settings ORDER BY key ASC;";

            var result = new List<SettingEntry>();
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                result.Add(new SettingEntry(reader.GetString(0), reader.GetString(1), reader.GetString(2)));
            }

            return result;
        }
    }

    public SettingEntry? Get(string key)
    {
        key = SettingsStoreGuard.NormalizeKey(key);
        lock (_gate)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "SELECT key, value, updated_at FROM product_settings WHERE key = $key;";
            cmd.Parameters.AddWithValue("$key", key);

            using var reader = cmd.ExecuteReader();
            return reader.Read()
                ? new SettingEntry(reader.GetString(0), reader.GetString(1), reader.GetString(2))
                : null;
        }
    }

    public SettingEntry Set(string key, string value)
    {
        key = SettingsStoreGuard.NormalizeKey(key);
        value = SettingsStoreGuard.NormalizeValue(value);
        var updatedAt = DateTimeOffset.UtcNow.ToString("o");

        lock (_gate)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = @"
INSERT INTO product_settings (key, value, created_at, updated_at)
VALUES ($key, $value, $updated, $updated)
ON CONFLICT(key) DO UPDATE SET
    value = excluded.value,
    updated_at = excluded.updated_at;";
            cmd.Parameters.AddWithValue("$key", key);
            cmd.Parameters.AddWithValue("$value", value);
            cmd.Parameters.AddWithValue("$updated", updatedAt);
            cmd.ExecuteNonQuery();
            return new SettingEntry(key, value, updatedAt);
        }
    }

    public bool Delete(string key)
    {
        key = SettingsStoreGuard.NormalizeKey(key);
        lock (_gate)
        {
            using var cmd = _connection.CreateCommand();
            cmd.CommandText = "DELETE FROM product_settings WHERE key = $key;";
            cmd.Parameters.AddWithValue("$key", key);
            return cmd.ExecuteNonQuery() > 0;
        }
    }

    private void MigrateLegacySettingsTable()
    {
        if (!TableExists(ProductPersistenceSchema.LegacySettingsTable)) return;

        Execute($@"
INSERT OR IGNORE INTO {ProductPersistenceSchema.ProductSettingsTable} (key, value, created_at, updated_at)
SELECT key, value, updated_at, updated_at
FROM {ProductPersistenceSchema.LegacySettingsTable};");
    }

    private bool TableExists(string tableName)
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = $name LIMIT 1;";
        cmd.Parameters.AddWithValue("$name", tableName);
        return cmd.ExecuteScalar() is not null;
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
