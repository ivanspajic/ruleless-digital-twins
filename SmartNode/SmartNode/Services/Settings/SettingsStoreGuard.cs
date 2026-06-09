using System.Text.RegularExpressions;

namespace SmartNode.Services.Settings;

internal static partial class SettingsStoreGuard
{
    private static readonly string[] CredentialMarkers =
    [
        "bearer",
        "token",
        "secret",
        "password",
        "api_key",
        "apikey",
        "access_token"
    ];

    public static string NormalizeKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Setting key is required.", nameof(key));
        }

        var trimmed = key.Trim();
        if (trimmed.Length > 128 || !SettingKeyPattern().IsMatch(trimmed))
        {
            throw new ArgumentException(
                "Setting key must be 1-128 characters using letters, numbers, dot, underscore, colon or hyphen.",
                nameof(key));
        }

        EnsureNoCredentialMarker(trimmed, "Setting key");
        return trimmed;
    }

    public static string NormalizeValue(string value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        if (value.Length > 4096)
        {
            throw new ArgumentException("Setting value must be 4096 characters or fewer.", nameof(value));
        }

        EnsureNoCredentialMarker(value, "Setting value");
        return value;
    }

    private static void EnsureNoCredentialMarker(string text, string label)
    {
        foreach (var marker in CredentialMarkers)
        {
            if (text.Contains(marker, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"{label} must not contain credential-like marker '{marker}'.");
            }
        }
    }

    [GeneratedRegex("^[A-Za-z0-9_.:-]+$")]
    private static partial Regex SettingKeyPattern();
}
