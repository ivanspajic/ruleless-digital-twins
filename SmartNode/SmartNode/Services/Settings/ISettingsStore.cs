namespace SmartNode.Services.Settings;

public sealed record SettingEntry(string Key, string Value, string UpdatedAt);

public interface ISettingsStore
{
    IReadOnlyList<SettingEntry> GetAll();
    SettingEntry? Get(string key);
    SettingEntry Set(string key, string value);
    bool Delete(string key);
}
