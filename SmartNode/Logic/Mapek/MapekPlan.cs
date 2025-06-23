using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Models;

namespace Logic.Mapek
{
    public class MapekPlan : IMapekPlan
    {
        private readonly ILogger<MapekPlan> _logger;

        public MapekPlan(IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetRequiredService<ILogger<MapekPlan>>();
        }

        public List<Models.Action> Plan(List<OptimalCondition> optimalConditions, List<Models.Action> actions)
        {
            _logger.LogInformation("Starting the Plan phase.");

            // 1. run simulations for all actions present
                // this should be done with some heuristics. spawn them all but don't run them all for the entire
                // duration of the simulation (e.g., 1h). cut this up into smaller, granular chunks, and then
                // continue with those that seem to get closer. we could outsource this deciding logic via a
                // user-defined delegate
            // 2. check the results of all remaining simulations at the end of the whole duration (e.g., 1h) and
            // pick those whose results meet their respective optimalconditions (and don't break other constraints)

            // this should be based on SSP rather than FMI because soft sensors can't necessarily be modeled with
            // FMUs



            return new List<Models.Action>();
        }
    }
}
