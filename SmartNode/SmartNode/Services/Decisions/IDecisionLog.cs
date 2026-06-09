using SmartNode.Models.MapeK;

namespace SmartNode.Services.Decisions;

// Rolling store of recent MAPE-K tick decisions (step 1.7). Every tick appends
// one entry; consumers read the most recent decisions (newest first) for the
// /api/mapek/decisions endpoint, demos, and reports.
public interface IDecisionLog
{
    void Append(DecisionLogEntry entry);

    // Most recent decisions, newest first, capped at `max`.
    IReadOnlyList<DecisionLogEntry> GetRecent(int max = 20);
}
