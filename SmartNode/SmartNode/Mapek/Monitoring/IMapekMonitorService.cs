using SmartNode.Models.MapeK;

namespace SmartNode.Mapek.Monitoring;

// Abstraction for the Monitor phase of the dry-run MAPE-K tick (issue #51).
// First step toward replacing the inline skeleton RuntimeState with an
// observable runtime state. A later PR can wire ObserveAsync to live Home
// Assistant state and the Nord Pool price provider without touching the
// endpoint.
public interface IMapekMonitorService
{
    Task<RuntimeState> ObserveAsync(CancellationToken cancellationToken = default);
}
