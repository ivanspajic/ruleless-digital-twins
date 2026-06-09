using SmartNode.Models.Goals;
using SmartNode.Models.MapeK;

namespace SmartNode.Mapek.Analysis;

// Minimal Analyzer phase for /api/mapek/tick (issue #59). Pure function over
// the Monitor's RuntimeState plus the active goals and the (currently empty)
// selected plan. The Analyzer never calls Home Assistant, never mutates state,
// and never triggers a plan — it only reports structured findings.
public interface IMapekAnalyzerService
{
    AnalysisResult Analyze(
        RuntimeState observedState,
        IReadOnlyList<UserGoal> activeGoals,
        ActionPlan selectedPlan
    );
}
