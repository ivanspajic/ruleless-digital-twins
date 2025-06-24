using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Models;
using Models.MapekModels;

namespace Logic.Mapek
{
    public class MapekPlan : IMapekPlan
    {
        private readonly ILogger<MapekPlan> _logger;

        public MapekPlan(IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetRequiredService<ILogger<MapekPlan>>();
        }

        public List<Models.Action> Plan(List<OptimalCondition> optimalConditions, List<Models.Action> actions, PropertyCache propertyCache)
        {
            _logger.LogInformation("Starting the Plan phase.");

            var plannedActions = new List<Models.Action>();

            var actuationActions = new List<ActuationAction>();
            var reconfigurationActions = new List<ReconfigurationAction>();

            foreach (var action in actions)
            {
                if (action is ActuationAction actuationAction)
                {
                    actuationActions.Add(actuationAction);
                }
                else
                {
                    reconfigurationActions.Add((ReconfigurationAction)action);
                }
            }

            var plannedActuationActions = SimulateAndPickBestActuationActions(actuationActions);
            var plannedReconfigurationActions = SimulateAndPickBestReconfigurationActions(reconfigurationActions);

            plannedActions.AddRange(plannedActuationActions);
            plannedActions.AddRange(plannedReconfigurationActions);

            return plannedActions;
        }

        private List<ActuationAction> SimulateAndPickBestActuationActions(List<ActuationAction> actuationActions)
        {
            var plannedActuationActions = new List<ActuationAction>();

            // 1. run simulations for all actions present
            // this should be done with some heuristics. spawn them all but don't run them all for the entire
            // duration of the simulation (e.g., 1h). cut this up into smaller, granular chunks, and then
            // continue with those that seem to get closer. we could outsource this deciding logic via a
            // user-defined delegate
            // 2. check the results of all remaining simulations at the end of the whole duration (e.g., 1h) and
            // pick those whose results meet their respective optimalconditions (and don't break other constraints)

            return plannedActuationActions;
        }

        private List<ReconfigurationAction> SimulateAndPickBestReconfigurationActions(List<ReconfigurationAction> reconfigurationActions)
        {
            var plannedReconfigurationActions = new List<ReconfigurationAction>();

            // TODO
            // for each action, run a simulation and gather the results. the results could be represented with an object containing
            // a bool - representing whether the simulation achieved restoring its optimalcondition constraint(s) and that it didn't
            // break others, and, in case they're related, values for properties that are being optimized
            // these simulation results should be compared such that the best optimized property values are selected
            // if there are other results that aren't related to the same properties, they should also be selected, but there should
            // only be one best result for every unique optimalcondition property

            return plannedReconfigurationActions;
        }
    }
}
