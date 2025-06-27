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
        private readonly IEqualityComparer<HashSet<Models.Action>> _actionSetEqualityComparer;

        public MapekPlan(IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetRequiredService<ILogger<MapekPlan>>();
            _factory = serviceProvider.GetRequiredService<IFactory>();
            _actionSetEqualityComparer = serviceProvider.GetRequiredService<IEqualityComparer<HashSet<Models.Action>>>();
        }

        public List<Models.Action> Plan(List<OptimalCondition> optimalConditions, List<Models.Action> actions, PropertyCache propertyCache)
        {
            _logger.LogInformation("Starting the Plan phase.");

            var plannedActions = new List<Models.Action>();

            // 1. run simulations for all actions present
            // this should be done with some heuristics. spawn them all but don't run them all for the entire
            // duration of the simulation (e.g., 1h). cut this up into smaller, granular chunks, and then
            // continue with those that seem to get closer. we could outsource this deciding logic via a
            // user-defined delegate
            // 2. check the results of all remaining simulations at the end of the whole duration (e.g., 1h) and
            // pick those whose results meet their respective optimalconditions (and don't break other constraints)

            // TODO
            // simulate all combinations of reconfigurationactions
                // to ensure a finite number of simulations, a granularity factor can be used on the parameters to simulate
                    // we can find the number of simulations for each configurableproperty by taking its min-max range and dividing
                    // by the granularity factor
                        // this is one type of hard-coded logic, however, we should make it possible to delegate this logic to the user
                // for each combination, check that the combination satisfies all optimalconditions
                    // if the combination satisfied optimalconditions, add it to the list of possible execution plans
            // out of the passing combinations, find the optimal combination per distinct property
                // this requires comparing the values of the optimized properties
                    // each property will need to have some form of user-defined precedence to assist with choosing the most
                    // optimal value outcome
                        // pick the one containing the most properties optimized
                        // then pick the one containing the property with the highest precedence
                        // then pick the first in the collection

            var actionCombinationSets = GetActionCombinations(actions);


            // Convert back to List<List<Models.Action>> for convenience.
            var actionCombinations = actionCombinationSets.Select(x => x.ToList())
                .ToList();

            return plannedActions;
        }

        private HashSet<HashSet<Models.Action>> GetActionCombinations(List<Models.Action> actions)
        {
            // Ensure that the set of sets has unique elements with the equality comparer.
            var actionCombinationSets = new HashSet<HashSet<Models.Action>>(_actionSetEqualityComparer);

            foreach (var action in actions)
            {
                // Pick the current Action out of the collection.
                var remainingActions = actions.Where(innerAction => innerAction != action)
                    .ToList();

                if (remainingActions.Count == 0)
                {
                    // If there are no remaining Actions in the collection, we have to create the set of
                    // Actions with the current Action and add it to the set of combinations.
                    var singleActionSet = new HashSet<Models.Action>
                    {
                        action
                    };

                    actionCombinationSets.Add(singleActionSet);
                }
                else
                {
                    // In case of more remaining Actions, we call this method again with the remaining Action
                    // collection and add the results to the set of combinations.
                    var remainingActionCombinations = GetActionCombinations(remainingActions);
                    actionCombinationSets.UnionWith(remainingActionCombinations);

                    foreach (var remainingActionCombination in remainingActionCombinations)
                    {
                        // For each Action combination from the collection of remaining Actions, create a new
                        // set and add the current Action to it before adding it to the set of combinations.
                        var multipleActionSet = new HashSet<Models.Action>();

                        multipleActionSet.UnionWith(remainingActionCombination);
                        multipleActionSet.Add(action);

                        actionCombinationSets.Add(multipleActionSet);
                    }
                }
            }

            return actionCombinationSets;
        }
    }
}
