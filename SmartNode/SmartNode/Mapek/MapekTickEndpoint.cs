using SmartNode.Mapek.Analysis;
using SmartNode.Mapek.Execution;
using SmartNode.Mapek.Monitoring;
using SmartNode.Models.Goals;
using SmartNode.Models.MapeK;
using SmartNode.Models.Simulation;
using SmartNode.Services.Decisions;
using SmartNode.Services.Execution;
using SmartNode.Services.Goals;
using SmartNode.Services.Safety;
using SmartNode.Services.Simulation;

namespace SmartNode.Mapek;

// Strongly-typed payload for /api/mapek/tick. Property order matches the JSON
// shape consumers see; new fields are appended (e.g. Analysis from issue #59)
// so existing consumers keep working.
internal sealed record MapekTickResponse(
    string Timestamp,
    bool DryRun,
    RuntimeState ObservedState,
    IReadOnlyList<UserGoal> ActiveGoals,
    IReadOnlyList<SimulationResult> SimulatedScenarios,
    ActionPlan SelectedPlan,
    IReadOnlyList<HaAction> Actions,
    string Explanation,
    IReadOnlyList<string> Warnings,
    AnalysisResult Analysis,
    DecisionLogEntry Decision
);

// Pure orchestration for the dry-run MAPE-K tick endpoint. Extracted from the
// inline HttpListener handler in Program.cs so it can be unit-tested offline
// without a live HTTP listener, Home Assistant, Docker, or filesystem.
internal static class MapekTickEndpoint
{
    internal const string ForcedDryRunWarning =
        "Real execution was requested (dryRun=false) but a safety gate blocked it; response forced to dry-run.";

    public static async Task<MapekTickResponse> BuildDryRunResponseAsync(
        string? rawBody,
        IMapekMonitorService monitor,
        IFutureSimulator simulator,
        IGoalRepository goalRepository,
        IMapekAnalyzerService analyzer,
        IDecisionLog decisionLog,
        ExecutionSettings? executionSettings = null,
        IHaActionExecutor? executor = null,
        SafetyRuntimeOptions? safety = null,
        IExecutionHistory? executionHistory = null,
        ISafetyEventLog? safetyEventLog = null,
        Func<DateTimeOffset>? clock = null,
        CancellationToken cancellationToken = default)
    {
        var now = (clock ?? (() => DateTimeOffset.UtcNow))();
        var requestedDryRun = ParseRequestedDryRun(rawBody);

        // P1 — best-effort safety-event audit. Recording NEVER changes safety
        // control flow: a log failure is swallowed here so the gate decision that
        // was already made still stands. Scenario/goal context is passed per call
        // because the winning scenario is not known at the master-gate stage.
        void RecordSafetyEvent(string outcome, string gate, string detail,
            HaAction? action = null, string? scenarioId = null, string? goalId = null)
        {
            if (safetyEventLog is null) return;
            try
            {
                safetyEventLog.Record(new SafetyEventRecord(
                    Timestamp: now,
                    Outcome: outcome,
                    Gate: gate,
                    Detail: detail,
                    EntityId: action?.EntityId,
                    Domain: action?.Domain,
                    Service: action?.Service,
                    ScenarioId: scenarioId,
                    GoalId: goalId));
            }
            catch { /* audit is best-effort; never affect actuation safety */ }
        }

        // Step 1.6 — real execution is fail-closed. It only happens when every
        // master gate holds (MAPEK_ALLOW_EXECUTION + dryRun=false + TOKEN_HA) AND
        // an executor is wired. Settings are passed in (not read from env here) so
        // this orchestration stays pure and testable.
        var exec = executionSettings ?? ExecutionSettings.Disabled;

        // P7-A — the global kill switch overrides every other gate. Permissive by
        // default, so this adds no behaviour change unless MAPEK_KILL_SWITCH is set.
        var safetyOptions = safety ?? SafetyRuntimeOptions.Permissive;
        var killSwitchOn = KillSwitchPolicy.BlocksRealExecution(safetyOptions);

        var realExecution = ExecutionPolicy.RealExecutionEnabled(exec, requestedDryRun)
            && !killSwitchOn
            && executor is not null;

        // Top-level Warnings are reserved for endpoint-level meta-warnings the caller
        // needs to act on (e.g. real execution was requested but blocked). Per-
        // observation warnings (HA unreachable, price provider unavailable) live on
        // the ObservedState so they stay attached to the data they describe.
        var warnings = new List<string>();
        if (!requestedDryRun && !realExecution)
        {
            warnings.Add(ForcedDryRunWarning);
            // The kill switch overrides everything, so it owns the explanation
            // whenever it is engaged — even if a master gate would also have blocked.
            var reason = killSwitchOn
                ? KillSwitchPolicy.ReasonText
                : ExecutionPolicy.BlockedReason(exec, requestedDryRun) ?? "executor not configured";
            warnings.Add($"Real execution blocked: {reason}.");
            RecordSafetyEvent(
                SafetyEventOutcome.Blocked,
                killSwitchOn ? SafetyGate.KillSwitch : SafetyGate.MasterGate,
                reason);
        }

        var activeGoals = goalRepository.GetAll().Where(g => g.Enabled).ToList();

        // Observed runtime state comes from the monitor abstraction (issue #51).
        // PR #56 wired IHaStateReader to read live `/api/states` from Home Assistant;
        // PR #57 wired IPriceForecastProvider for the live price. The monitor emits
        // its own per-observation warnings on RuntimeState.Warnings — see
        // MapekMonitorService.HaSnapshotUnavailableWarning et al.
        var observedState = await monitor.ObserveAsync(cancellationToken);

        var simReq = new SimulationRequest(
            Timestamp: DateTimeOffset.UtcNow,
            DryRun: true,
            ObservedState: observedState,
            ActiveGoals: activeGoals);

        var scenarios = await simulator.SimulateAsync(simReq, cancellationToken);

        // Step 1.4 — select the highest-scoring scenario (argmax) instead of
        // blindly taking the first. Stable first-wins tie-break via Aggregate.
        var winner = scenarios.Count > 0
            ? scenarios.Aggregate((best, c) => c.Score > best.Score ? c : best)
            : null;

        var sourceGoal = activeGoals.FirstOrDefault();

        // Step 1.5 + 1.4 — actions are now conditional on the winning decision:
        // only "heat-now" stages the goal's Home Assistant actions; "do-nothing"
        // and "wait-cheaper" intentionally stage nothing. Still purely
        // consultative — nothing is sent to HA and every action is Executed=false.
        var shouldAct = sourceGoal is not null
            && winner is not null
            && winner.ScenarioId == SimpleFutureSimulator.ScenarioHeatNow;

        var plannedActions = shouldAct
            ? sourceGoal!.Actions
                .Select(a => new HaAction(a.Domain, a.Service, a.EntityId, a.Data, Executed: false))
                .ToList()
            : new List<HaAction>();

        // Step 1.6 — when real execution is enabled, run each staged action through
        // the per-action allowlist and the executor. Anything denied or failing
        // stays Executed=false with an explicit warning. When disabled, the plan
        // remains a pure dry-run (every action Executed=false).
        var effectiveDryRun = !realExecution;
        List<HaAction> finalActions;
        if (realExecution && plannedActions.Count > 0)
        {
            // P4-G — live-snapshot presence gate. An action is never executable
            // against an entity that is absent from the observed snapshot, even if
            // the operator allowlist would permit it. Fail-closed: an empty/missing
            // snapshot makes nothing executable.
            var presentEntities = new HashSet<string>(
                observedState.HaEntitiesSnapshot.Keys, StringComparer.OrdinalIgnoreCase);

            finalActions = new List<HaAction>(plannedActions.Count);

            // P7-B / P7-B2 — the time-windowed policies (cooldown, rate limit) read
            // the cross-tick execution history. Seed a local working copy so actions
            // executed earlier in THIS tick also count toward the limits. If a limit
            // is active and the history cannot be read, FAIL CLOSED: refuse all real
            // actions this tick rather than actuate without enforceable safety.
            var historyNeeded = safetyOptions.ActionCooldownSeconds > 0
                || safetyOptions.MaxActionsPerHour > 0
                || safetyOptions.MaxActionsPerEntityPerHour > 0;
            var recentExec = new List<ActionExecutionRecord>();
            var historyUnavailable = false;
            if (historyNeeded)
            {
                try
                {
                    recentExec.AddRange(executionHistory?.GetRecent()
                        ?? (IReadOnlyList<ActionExecutionRecord>)Array.Empty<ActionExecutionRecord>());
                }
                catch (Exception ex)
                {
                    historyUnavailable = true;
                    warnings.Add($"Real execution blocked: execution history unavailable, cannot enforce cooldown/rate-limit safely ({ex.Message}).");
                    RecordSafetyEvent(
                        SafetyEventOutcome.Blocked, SafetyGate.HistoryUnavailable,
                        $"execution history unavailable: {ex.Message}",
                        scenarioId: winner?.ScenarioId, goalId: sourceGoal?.Id);
                }
            }

            foreach (var action in plannedActions)
            {
                if (historyUnavailable)
                {
                    finalActions.Add(action); // fail-closed: stays Executed=false
                    continue;
                }

                if (!ExecutionPolicy.IsTargetPresent(action, presentEntities))
                {
                    warnings.Add($"Action {action.Domain}.{action.Service} on {action.EntityId} not executed: entity absent from the live Home Assistant snapshot.");
                    RecordSafetyEvent(SafetyEventOutcome.Blocked, SafetyGate.TargetAbsent,
                        "entity absent from the live Home Assistant snapshot",
                        action, winner?.ScenarioId, sourceGoal?.Id);
                    finalActions.Add(action); // stays Executed=false
                    continue;
                }

                var (allowed, reason) = ExecutionPolicy.EvaluateAction(exec, action);
                if (!allowed)
                {
                    warnings.Add($"Action {action.Domain}.{action.Service} on {action.EntityId} not executed: {reason}.");
                    RecordSafetyEvent(SafetyEventOutcome.Blocked, SafetyGate.Allowlist, reason,
                        action, winner?.ScenarioId, sourceGoal?.Id);
                    finalActions.Add(action);
                    continue;
                }

                // P7-B — cooldown then rate limit, evaluated against the execution
                // history. Both are disabled by default (Permissive), so this adds
                // no behaviour change unless the operator sets the limits.
                var cooldown = ActionCooldownPolicy.Evaluate(safetyOptions, action, recentExec, now);
                if (!cooldown.Allowed)
                {
                    warnings.Add($"Action {action.Domain}.{action.Service} on {action.EntityId} not executed: {cooldown.Detail}.");
                    RecordSafetyEvent(SafetyEventOutcome.Blocked, SafetyGate.Cooldown, cooldown.Detail,
                        action, winner?.ScenarioId, sourceGoal?.Id);
                    finalActions.Add(action);
                    continue;
                }

                var rateLimit = ActionRateLimitPolicy.Evaluate(safetyOptions, action, recentExec, now);
                if (!rateLimit.Allowed)
                {
                    warnings.Add($"Action {action.Domain}.{action.Service} on {action.EntityId} not executed: {rateLimit.Detail}.");
                    RecordSafetyEvent(SafetyEventOutcome.Blocked, SafetyGate.RateLimit, rateLimit.Detail,
                        action, winner?.ScenarioId, sourceGoal?.Id);
                    finalActions.Add(action);
                    continue;
                }

                var result = await executor!.ExecuteAsync(action, cancellationToken);
                if (!result.Executed)
                {
                    warnings.Add($"Action {action.Domain}.{action.Service} on {action.EntityId} execution failed: {result.Error}.");
                    RecordSafetyEvent(SafetyEventOutcome.Failed, SafetyGate.Execution,
                        $"execution failed: {result.Error}",
                        action, winner?.ScenarioId, sourceGoal?.Id);
                }
                else
                {
                    RecordSafetyEvent(SafetyEventOutcome.Executed, SafetyGate.Execution,
                        "executed against Home Assistant",
                        action, winner?.ScenarioId, sourceGoal?.Id);
                    // Only successful real executions enter the history (so cooldown
                    // and rate limit count real actuations, not attempts). Recording
                    // is best-effort: a store failure is surfaced as a warning but
                    // does not undo the actuation that already happened.
                    var record = new ActionExecutionRecord(
                        action.EntityId, action.Domain, action.Service, now,
                        ScenarioId: winner?.ScenarioId, GoalId: sourceGoal?.Id);
                    try { executionHistory?.Record(record); }
                    catch (Exception ex)
                    {
                        warnings.Add($"Action {action.Domain}.{action.Service} on {action.EntityId} executed but not recorded in history ({ex.Message}).");
                    }
                    recentExec.Add(record);
                }
                finalActions.Add(action with { Executed = result.Executed });
            }
        }
        else
        {
            finalActions = plannedActions;
        }

        var selectedPlan = new ActionPlan(
            ScenarioId: winner?.ScenarioId ?? "no-scenario",
            Rationale: BuildRationale(winner, sourceGoal, finalActions, effectiveDryRun),
            Actions: finalActions,
            DryRun: effectiveDryRun);

        // Minimal Analyzer phase (issue #59) — pure function over the Monitor's
        // RuntimeState, the active goals, and the (currently empty) plan. It does
        // not act, mutate, or call HA; it only emits stable-coded findings.
        var analysis = analyzer.Analyze(observedState, activeGoals, selectedPlan);

        var nowIso = DateTimeOffset.UtcNow.ToString("o");
        var explanation = winner?.Description
            ?? "Dry-run skeleton: heuristic simulator returned no scenarios.";

        // Step 1.7 — record a compact, append-only trace of this decision. The
        // log is in-memory and bounded; appending never throws on a full buffer.
        var decision = new DecisionLogEntry(
            Timestamp: nowIso,
            GoalId: sourceGoal?.Id,
            SelectedScenario: selectedPlan.ScenarioId,
            ObservedPriceNokPerKwh: observedState.CurrentPriceNokPerKwh,
            Actions: selectedPlan.Actions,
            DryRun: effectiveDryRun,
            Explanation: explanation);
        decisionLog.Append(decision);

        return new MapekTickResponse(
            Timestamp: nowIso,
            DryRun: effectiveDryRun,
            ObservedState: observedState,
            ActiveGoals: activeGoals,
            SimulatedScenarios: scenarios,
            SelectedPlan: selectedPlan,
            Actions: selectedPlan.Actions,
            Explanation: explanation,
            Warnings: warnings,
            Analysis: analysis,
            Decision: decision);
    }

    // Human-readable, machine-stable rationale for the selected plan.
    private static string BuildRationale(
        Models.Simulation.SimulationResult? winner,
        Models.Goals.UserGoal? goal,
        IReadOnlyList<HaAction> actions,
        bool dryRun)
    {
        if (goal is null)
        {
            return "Dry-run tick: no active goal, so no actions planned.";
        }
        if (winner is null)
        {
            return $"Tick for goal '{goal.Id}': simulator produced no scenarios.";
        }

        var score = winner.Score.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);
        if (winner.ScenarioId != SimpleFutureSimulator.ScenarioHeatNow)
        {
            return $"Plan for goal '{goal.Id}': selected '{winner.ScenarioId}' (score {score}); no actions staged.";
        }

        if (dryRun)
        {
            return $"Dry-run plan for goal '{goal.Id}': selected 'heat-now' (score {score}); " +
                   $"{actions.Count} action(s) staged (Executed=false); nothing sent to Home Assistant.";
        }

        var executed = actions.Count(a => a.Executed);
        return $"Plan for goal '{goal.Id}': selected 'heat-now' (score {score}); " +
               $"{executed}/{actions.Count} action(s) executed against Home Assistant.";
    }

    private static bool ParseRequestedDryRun(string? rawBody)
    {
        if (string.IsNullOrWhiteSpace(rawBody)) return true;
        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(rawBody);
            if (doc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                && doc.RootElement.TryGetProperty("dryRun", out var dr)
                && dr.ValueKind == System.Text.Json.JsonValueKind.False)
            {
                return false;
            }
        }
        catch (System.Text.Json.JsonException)
        {
            // Malformed body: keep the safe default (dryRun=true).
        }
        return true;
    }
}
