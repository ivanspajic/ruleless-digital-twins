using System.Globalization;

namespace SmartNode.Mapek.Execution;

// Non-secret persisted setting keys that layer on top of the env-based safety
// controls. They live in the ISettingsStore (which structurally rejects
// credential-like keys/values), so TOKEN_HA is intentionally NOT among them —
// the token always comes from the environment.
public static class SafetySettingKeys
{
    public const string AllowExecution = "mapek.allow_execution";
    public const string KillSwitch = "mapek.kill_switch";
    public const string AllowedEntities = "mapek.allowed_entities";
    public const string AllowedServices = "mapek.allowed_services";
    public const string MaxActionsPerHour = "mapek.max_actions_per_hour";
    public const string CooldownSeconds = "mapek.cooldown_seconds";
    public const string AutonomousExecutionEnabled = "mapek.autonomous_execution_enabled";
}

// Result of merging environment variables with the persisted settings store.
// AutonomousExecutionEnabled is surfaced for completeness but the autonomous
// loop stays dry-run in this build (real autonomous actuation is a later step);
// it never relaxes the manual-tick gates.
public sealed record ResolvedSafetySettings(
    ExecutionSettings Execution,
    SafetyRuntimeOptions Safety,
    bool AutonomousExecutionEnabled,
    IReadOnlyList<string> Warnings);

// Pure merge of env vars and non-secret persisted settings into the existing
// ExecutionSettings / SafetyRuntimeOptions snapshots. No I/O, no clock, no env
// reads except through the injected getter — fully unit-testable.
//
// Safety contract (PR 4):
//   * Fail-closed default preserved: with no env and no persisted values,
//     execution is Disabled (AllowExecution=false, empty allowlists).
//   * Environment variables remain supported: absent persisted values leave the
//     env-derived behaviour exactly as before.
//   * Invalid persisted values never open execution. Booleans only ENABLE on an
//     explicit valid true; numeric limits only ever TIGHTEN; an unparseable
//     value is ignored with a warning.
public static class SafetySettingsResolver
{
    private static readonly IReadOnlyDictionary<string, string> Empty =
        new Dictionary<string, string>();

    public static ResolvedSafetySettings Resolve(
        Func<string, string?> getEnv,
        IReadOnlyDictionary<string, string>? persisted)
    {
        ArgumentNullException.ThrowIfNull(getEnv);
        persisted ??= Empty;
        var warnings = new List<string>();

        // --- environment baseline (unchanged from the pre-PR4 inline parsing) ---
        var allow = ParseBool(getEnv("MAPEK_ALLOW_EXECUTION"));
        var token = getEnv("TOKEN_HA") ?? string.Empty;
        var entities = ParseCsv(getEnv("MAPEK_ALLOWED_ENTITIES"));
        var services = ParseCsv(getEnv("MAPEK_ALLOWED_SERVICES"));
        var kill = ParseBool(getEnv("MAPEK_KILL_SWITCH"));
        var cooldown = ParseNonNegative(getEnv("MAPEK_ACTION_COOLDOWN_SECONDS"));
        var maxPerHour = ParseNonNegative(getEnv("MAPEK_MAX_ACTIONS_PER_HOUR"));
        var maxPerEntity = ParseNonNegative(getEnv("MAPEK_MAX_ACTIONS_PER_ENTITY_PER_HOUR"));
        var autoExec = ParseBool(getEnv("MAPEK_AUTONOMOUS_EXECUTION"));

        // --- boolean enablers: OR(env, persisted valid true); invalid → ignore + warn ---
        allow = OrPersistedBool(persisted, SafetySettingKeys.AllowExecution, allow, warnings);
        kill = OrPersistedBool(persisted, SafetySettingKeys.KillSwitch, kill, warnings);
        autoExec = OrPersistedBool(persisted, SafetySettingKeys.AutonomousExecutionEnabled, autoExec, warnings);

        // --- allowlists: persisted overrides env when the key is present. An empty
        //     persisted value parses to an empty set, i.e. deny-all (fail-closed). ---
        if (persisted.TryGetValue(SafetySettingKeys.AllowedEntities, out var rawEntities))
            entities = ParseCsv(rawEntities);
        if (persisted.TryGetValue(SafetySettingKeys.AllowedServices, out var rawServices))
            services = ParseCsv(rawServices);

        // --- numeric limits: most-restrictive-wins so persisted can only tighten ---
        cooldown = TightenCooldown(persisted, cooldown, warnings);
        maxPerHour = TightenHourlyLimit(persisted, maxPerHour, warnings);

        var execution = new ExecutionSettings(
            AllowExecution: allow,
            TokenPresent: !string.IsNullOrWhiteSpace(token),
            AllowedEntities: entities,
            AllowedServices: services);

        var safety = new SafetyRuntimeOptions(
            KillSwitchEngaged: kill,
            ActionCooldownSeconds: cooldown,
            MaxActionsPerHour: maxPerHour,
            MaxActionsPerEntityPerHour: maxPerEntity);

        return new ResolvedSafetySettings(execution, safety, autoExec, warnings);
    }

    // Returns env value OR a persisted explicit true. Persisted false leaves the
    // env value (OR with false). An unparseable persisted value is ignored with a
    // warning so a typo can never flip a safety toggle.
    private static bool OrPersistedBool(
        IReadOnlyDictionary<string, string> persisted, string key, bool envValue, List<string> warnings)
    {
        if (!persisted.TryGetValue(key, out var raw)) return envValue;
        var parsed = TryParseBool(raw);
        if (parsed is null)
        {
            warnings.Add($"Ignoring invalid persisted setting '{key}'='{raw}' (expected true/false); using environment value.");
            return envValue;
        }
        return envValue || parsed.Value;
    }

    // Cooldown: longer is safer, so take the maximum of env and persisted.
    private static int TightenCooldown(
        IReadOnlyDictionary<string, string> persisted, int envValue, List<string> warnings)
    {
        if (!TryGetPersistedNonNegative(persisted, SafetySettingKeys.CooldownSeconds, warnings, out var p))
            return envValue;
        return Math.Max(envValue, p);
    }

    // Hourly action cap with 0 == unlimited. Most-restrictive = the smaller
    // positive limit; an unlimited side never overrides a positive limit.
    private static int TightenHourlyLimit(
        IReadOnlyDictionary<string, string> persisted, int envValue, List<string> warnings)
    {
        if (!TryGetPersistedNonNegative(persisted, SafetySettingKeys.MaxActionsPerHour, warnings, out var p))
            return envValue;
        if (envValue == 0) return p;          // env unlimited → persisted decides
        if (p == 0) return envValue;          // persisted unlimited → keep the env limit
        return Math.Min(envValue, p);         // both limited → tighter wins
    }

    // True only when the key is present AND parses to a non-negative int. A
    // present-but-invalid value is ignored with a warning (never tightens or
    // loosens on garbage).
    private static bool TryGetPersistedNonNegative(
        IReadOnlyDictionary<string, string> persisted, string key, List<string> warnings, out int value)
    {
        value = 0;
        if (!persisted.TryGetValue(key, out var raw)) return false;
        if (int.TryParse((raw ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) && n >= 0)
        {
            value = n;
            return true;
        }
        warnings.Add($"Ignoring invalid persisted setting '{key}'='{raw}' (expected a non-negative integer); using environment value.");
        return false;
    }

    private static bool? TryParseBool(string? raw)
    {
        var v = raw?.Trim();
        if (string.IsNullOrEmpty(v)) return null;
        if (v.Equals("1", StringComparison.OrdinalIgnoreCase) || v.Equals("true", StringComparison.OrdinalIgnoreCase)
            || v.Equals("yes", StringComparison.OrdinalIgnoreCase) || v.Equals("on", StringComparison.OrdinalIgnoreCase))
            return true;
        if (v.Equals("0", StringComparison.OrdinalIgnoreCase) || v.Equals("false", StringComparison.OrdinalIgnoreCase)
            || v.Equals("no", StringComparison.OrdinalIgnoreCase) || v.Equals("off", StringComparison.OrdinalIgnoreCase))
            return false;
        return null;
    }

    private static bool ParseBool(string? raw) => TryParseBool(raw) ?? false;

    private static int ParseNonNegative(string? raw)
        => int.TryParse(raw?.Trim(), out var n) && n > 0 ? n : 0;

    private static HashSet<string> ParseCsv(string? raw)
        => (raw ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
}
