namespace SmartNode.Mapek.Execution;

// Why a real Home Assistant execution was refused by a P7-A safety policy.
public enum SafetyRejectionReason
{
    None = 0,
    KillSwitch,
    Cooldown,
    GlobalRateLimit,
    EntityRateLimit
}

// Pure result of a safety policy evaluation. Fail-closed callers treat
// Allowed=false as "do not execute" and surface Detail as an explicit warning.
public sealed record SafetyDecision(bool Allowed, SafetyRejectionReason Reason, string Detail)
{
    public static SafetyDecision Allow { get; } =
        new(true, SafetyRejectionReason.None, "allowed");

    public static SafetyDecision Reject(SafetyRejectionReason reason, string detail) =>
        new(false, reason, detail);
}
