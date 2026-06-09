using SmartNode.Models.MapeK;

namespace SmartNode.Mapek.Execution;

// Outcome of attempting one Home Assistant service call.
public sealed record HaExecutionResult(bool Executed, int? StatusCode, string? Error);

// Sends a single HaAction to Home Assistant. Implementations MUST NOT throw:
// network/HA failures come back as HaExecutionResult(Executed: false, Error: ...)
// so the tick can surface them as warnings without crashing the request.
public interface IHaActionExecutor
{
    Task<HaExecutionResult> ExecuteAsync(HaAction action, CancellationToken cancellationToken = default);
}
