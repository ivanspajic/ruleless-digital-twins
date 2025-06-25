using Logic.FactoryInterface;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Models;
using Models.MapekModels;

namespace Logic.Mapek
{
    public class MapekPlan : IMapekPlan
    {
        private readonly ILogger<MapekPlan> _logger;
        private readonly IFactory _factory;

        public MapekPlan(IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetRequiredService<ILogger<MapekPlan>>();
            _factory = serviceProvider.GetRequiredService<IFactory>();
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
            // simulate all combinations of reconfigurationactions
                // to ensure a finite number of simulations, a granularity factor can be used on the parameters to simulate
                    // we can find the number of simulations for each configurableproperty by taking its min-max range and dividing
                    // by the granularity factor
                        // this is one type of hard-coded logic, however, we should make it possible to delegate the logic to the user
                // for each combination, check that the combination satisfies all optimalconditions
                    // if the combination satisfied optimalconditions, add it to the list of possible execution plans
            // out of the passing combinations, find the optimal combination per distinct property
                // this requires comparing the values of the optimized properties
                    // each property will need to have some form of user-defined precedence to assist with choosing the most
                    // optimal value outcome
                        // pick the one containing the most properties optimized
                        // then pick the one containing the property with the highest precedence
                        // then pick the first in the collection

            

            return plannedReconfigurationActions;
        }
    }
}
