using SmartNode.Mapek.Execution;

namespace SmartNode.Services.Execution;

// P7-B — cross-tick record of real Home Assistant executions. It backs the
// time-windowed safety policies (cooldown, rate limit): the tick records every
// successful real execution here, and reads recent records back to evaluate the
// policies on the next attempt. Only successful real executions are recorded —
// dry-run, denied, and failed actions never enter the history.
public interface IExecutionHistory
{
    void Record(ActionExecutionRecord record);

    // Recent execution records (bounded). The time-windowed policies filter this
    // list by their own window relative to the tick's "now".
    IReadOnlyList<ActionExecutionRecord> GetRecent();
}
