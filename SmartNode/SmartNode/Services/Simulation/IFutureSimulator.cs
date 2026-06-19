using SmartNode.Models.Simulation;

namespace SmartNode.Services.Simulation;

public interface IFutureSimulator
{
    Task<IReadOnlyList<SimulationResult>> SimulateAsync(
        SimulationRequest request,
        CancellationToken cancellationToken = default);
}
