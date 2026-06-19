using SmartNode.Mapek.Analysis;
using SmartNode.Mapek.Execution;
using SmartNode.Mapek.Monitoring;
using SmartNode.Services.Decisions;
using SmartNode.Services.Goals;
using SmartNode.Services.Simulation;

namespace SmartNode.Mapek.Autonomous;

// Factory for the tick action the autonomous loop runs (step 1.8). It calls the
// exact same MAPE-K logic as POST /api/mapek/tick, but execution is HARD-disabled
// (ExecutionSettings.Disabled, no executor) so the autonomous loop can never
// actuate Home Assistant — observe/analyze/score/plan/log only. Combining the
// autonomous loop with real execution (step 1.6) is a deliberate separate step.
public static class AutonomousTick
{
    public static Func<CancellationToken, Task> CreateDryRunTick(
        IMapekMonitorService monitor,
        IFutureSimulator simulator,
        IGoalRepository goalRepository,
        IMapekAnalyzerService analyzer,
        IDecisionLog decisionLog)
        => async cancellationToken =>
        {
            await MapekTickEndpoint.BuildDryRunResponseAsync(
                rawBody: null,
                monitor,
                simulator,
                goalRepository,
                analyzer,
                decisionLog,
                executionSettings: ExecutionSettings.Disabled,
                executor: null,
                safety: null, // autonomous tick stays dry-run; no execution to gate
                cancellationToken: cancellationToken);
        };
}
