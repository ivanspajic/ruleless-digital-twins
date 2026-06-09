using System.Text.Json;

namespace SmartNode.Services.Settings;

internal sealed record SettingsEndpointResult(int StatusCode, object? Payload);

internal static class SettingsEndpoint
{
    public static SettingsEndpointResult List(ISettingsStore store)
    {
        var settings = store.GetAll();
        return new SettingsEndpointResult(200, new { count = settings.Count, settings });
    }

    public static SettingsEndpointResult Get(ISettingsStore store, string key)
    {
        try
        {
            var setting = store.Get(key);
            return setting is null
                ? new SettingsEndpointResult(404, new { error = $"Setting '{key}' not found." })
                : new SettingsEndpointResult(200, setting);
        }
        catch (ArgumentException ex)
        {
            return new SettingsEndpointResult(400, new { error = ex.Message });
        }
    }

    public static SettingsEndpointResult Set(ISettingsStore store, string key, string? rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return new SettingsEndpointResult(400, new { error = "Request body is required." });
        }

        string? value;
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            if (!doc.RootElement.TryGetProperty("value", out var valueEl) || valueEl.ValueKind != JsonValueKind.String)
            {
                return new SettingsEndpointResult(400, new { error = "Body must be a JSON object with a string 'value'." });
            }

            value = valueEl.GetString();
        }
        catch (JsonException ex)
        {
            return new SettingsEndpointResult(400, new { error = $"Invalid JSON: {ex.Message}" });
        }

        try
        {
            return new SettingsEndpointResult(200, store.Set(key, value ?? string.Empty));
        }
        catch (ArgumentException ex)
        {
            return new SettingsEndpointResult(400, new { error = ex.Message });
        }
    }

    public static SettingsEndpointResult Update(ISettingsStore store, string? rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody))
        {
            return new SettingsEndpointResult(400, new { error = "Request body is required." });
        }

        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new SettingsEndpointResult(400, new { error = "Body must be a JSON object." });
            }

            var entries = ParseEntries(doc.RootElement);
            if (entries.Count == 0)
            {
                return new SettingsEndpointResult(400, new { error = "Body must contain at least one setting." });
            }

            var normalized = new List<(string Key, string Value)>();
            foreach (var (key, value) in entries)
            {
                normalized.Add((
                    SettingsStoreGuard.NormalizeKey(key),
                    SettingsStoreGuard.NormalizeValue(value)));
            }

            var updated = new List<SettingEntry>();
            foreach (var (key, value) in normalized)
            {
                updated.Add(store.Set(key, value));
            }

            return new SettingsEndpointResult(200, new { count = updated.Count, settings = updated });
        }
        catch (JsonException ex)
        {
            return new SettingsEndpointResult(400, new { error = $"Invalid JSON: {ex.Message}" });
        }
        catch (ArgumentException ex)
        {
            return new SettingsEndpointResult(400, new { error = ex.Message });
        }
    }

    public static SettingsEndpointResult Delete(ISettingsStore store, string key)
    {
        try
        {
            return store.Delete(key)
                ? new SettingsEndpointResult(200, new { deleted = true, key })
                : new SettingsEndpointResult(404, new { error = $"Setting '{key}' not found." });
        }
        catch (ArgumentException ex)
        {
            return new SettingsEndpointResult(400, new { error = ex.Message });
        }
    }

    private static List<(string Key, string Value)> ParseEntries(JsonElement root)
    {
        var hasKey = root.TryGetProperty("key", out var keyEl);
        var hasValue = root.TryGetProperty("value", out var valueEl);
        if (hasKey || hasValue)
        {
            if (!hasKey || keyEl.ValueKind != JsonValueKind.String
                || !hasValue || valueEl.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("Body must include string 'key' and string 'value'.");
            }

            return new List<(string, string)>
            {
                (keyEl.GetString() ?? string.Empty, valueEl.GetString() ?? string.Empty)
            };
        }

        if (!root.TryGetProperty("settings", out var settingsEl) || settingsEl.ValueKind != JsonValueKind.Object)
        {
            throw new ArgumentException("Body must include either string 'key'/'value' or a 'settings' object.");
        }

        var entries = new List<(string, string)>();
        foreach (var prop in settingsEl.EnumerateObject())
        {
            if (prop.Value.ValueKind != JsonValueKind.String)
            {
                throw new ArgumentException("Every setting value must be a string.");
            }
            entries.Add((prop.Name, prop.Value.GetString() ?? string.Empty));
        }
        return entries;
    }
}
