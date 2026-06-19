namespace SmartNode.Services.Settings;

public sealed class InMemorySettingsStore : ISettingsStore
{
    private readonly object _gate = new();
    private readonly Dictionary<string, SettingEntry> _entries = new(StringComparer.Ordinal);

    public IReadOnlyList<SettingEntry> GetAll()
    {
        lock (_gate)
        {
            return _entries.Values.OrderBy(e => e.Key, StringComparer.Ordinal).ToList();
        }
    }

    public SettingEntry? Get(string key)
    {
        key = SettingsStoreGuard.NormalizeKey(key);
        lock (_gate)
        {
            return _entries.TryGetValue(key, out var entry) ? entry : null;
        }
    }

    public SettingEntry Set(string key, string value)
    {
        key = SettingsStoreGuard.NormalizeKey(key);
        value = SettingsStoreGuard.NormalizeValue(value);
        var entry = new SettingEntry(key, value, DateTimeOffset.UtcNow.ToString("o"));

        lock (_gate)
        {
            _entries[key] = entry;
            return entry;
        }
    }

    public bool Delete(string key)
    {
        key = SettingsStoreGuard.NormalizeKey(key);
        lock (_gate)
        {
            return _entries.Remove(key);
        }
    }
}
