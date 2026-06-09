namespace SmartNode.Mapek.Execution;

// One past real execution, consumed by the time-based safety policies (cooldown,
// rate limit). Pure data: the policies receive the history explicitly so they
// stay clock-free and unit-testable. The (EntityId, Domain, Service) triple is
// the cooldown subject; EntityId alone is the per-entity rate-limit subject.
// ScenarioId / GoalId are optional audit context (persisted by the SQLite store);
// the safety policies never use them.
public sealed record ActionExecutionRecord(
    string EntityId,
    string Domain,
    string Service,
    DateTimeOffset ExecutedAt,
    string? ScenarioId = null,
    string? GoalId = null);
