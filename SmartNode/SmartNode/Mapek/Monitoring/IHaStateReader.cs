namespace SmartNode.Mapek.Monitoring;

// Minimal abstraction over Home Assistant's /api/states (issue #52). The MAPE-K
// monitor uses this to populate RuntimeState.HaEntitiesSnapshot. Implementations
// MUST return null on any failure (HA unreachable, missing token, malformed
// response, timeout, ...) — they MUST NOT throw — so the dry-run tick can
// degrade gracefully into the "snapshot unavailable" path.
public interface IHaStateReader
{
    Task<IReadOnlyDictionary<string, object?>?> ReadAllAsync(CancellationToken cancellationToken = default);
}
