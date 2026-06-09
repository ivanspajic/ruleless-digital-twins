namespace SmartNode.Mapek.Execution;

// Immutable snapshot of the execution gate, built from the environment at
// request time and passed into the tick so the endpoint itself stays pure and
// unit-testable (no env reads inside the orchestration). Fail-closed: the
// default `Disabled` value never permits a real Home Assistant call.
public sealed record ExecutionSettings(
    bool AllowExecution,
    bool TokenPresent,
    IReadOnlySet<string> AllowedEntities,
    IReadOnlySet<string> AllowedServices)
{
    public static ExecutionSettings Disabled { get; } =
        new(false, false, new HashSet<string>(), new HashSet<string>());
}
