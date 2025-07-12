using Logic.Models.MapekModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Logic.Models.OntologicalModels;
using Logic.Mapek.EqualityComparers;
using System.Net.Http.Headers;

namespace Logic.Mapek
{
    internal class MapekPlan : IMapekPlan
    {
        private readonly ILogger<MapekPlan> _logger;

        public MapekPlan(IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetRequiredService<ILogger<MapekPlan>>();
        }

        public IEnumerable<Models.OntologicalModels.Action> Plan(IEnumerable<OptimalCondition> optimalConditions,
            IEnumerable<Models.OntologicalModels.Action> actions,
            PropertyCache propertyCache)
        {
            _logger.LogInformation("Starting the Plan phase.");

            var simulationGranularity = 4;
            var plannedActions = new List<Models.OntologicalModels.Action>();

            // The two Action types should be split to facilitate simulations. ActuationActions may be de/activated at any point during
            // the available time to restore an OptimalCondition. On the other hand, ReconfigurationActions aren't dependent on a time
            // factor (although the underlying soft Sensor algorithms may take long to run) but will nonetheless be included in simulation
            // configurations to ensure that all OptimalConditions are met when simulating different types fo Actions in conjunction.
            // To avoid generating duplicate combinations for simulation ticks (intervals) when removing ReconfigurationActions,
            // generating the combinations of the two types should be done separately.
            var actuationActions = actions.Where(action => action is ActuationAction);
            var reconfigurationActions = actions.Where(action => action is ReconfigurationAction);

            // Get all possible combinations for ActuationActions.
            var actuationActionCombinations = GetActuationActionCombinations(actuationActions);

            // Get all possible combinations for ReconfigurationActions.
            var reconfigurationActionCombinations = GetReconfigurationActionCombinations(reconfigurationActions);

            // Get all possible simulation configurations for the given Actions.
            var simulationConfigurations = GetSimulationConfigurationsFromActionCombinations(actuationActionCombinations,
                reconfigurationActionCombinations,
                simulationGranularity);

            //var simulationResults = Simulate(simulationConfigurations, optimalConditions, propertyCache);

            // TODO:
            // for each combination, check that the combination satisfies all optimalconditions
                    // if the combination satisfied optimalconditions, add it to the list of possible execution plans
            // out of the passing combinations, find the optimal combination per distinct property
                // this requires comparing the values of the optimized properties
                    // pick the one containing the most properties optimized
                    // pick the one with the least number of actions
                    // then pick the first in the collection

            // TODO: get the minimal number of time from all optimal conditions to use as the maximum for the simulations

            return plannedActions;
        }

        private IEnumerable<IEnumerable<ActuationAction>> GetActuationActionCombinations(IEnumerable<Models.OntologicalModels.Action> actions)
        {
            var actuationActionCombinations = new HashSet<HashSet<Models.OntologicalModels.Action>>(new ActionSetEqualityComparer());

            var actuationActions = actions.Select(action => action as ActuationAction);

            // Group ActuationActions by their Actuators to allow creating combinations that don't consist of states of the same Actuator.
            var actuationActionsByActuatorMap = new Dictionary<string, List<ActuationAction>>();

            foreach (var actuationAction in actuationActions)
            {
                if (actuationActionsByActuatorMap.TryGetValue(actuationAction.ActuatorState.Actuator.Name, out List<ActuationAction> actuationActionsInMap))
                {
                    actuationActionsInMap.Add(actuationAction);
                }
                else
                {
                    actuationActionsByActuatorMap.Add(actuationAction.ActuatorState.Actuator.Name, new List<ActuationAction>
                    {
                        actuationAction
                    });
                }
            }

            // Convert to a simple collection.
            var actuatorActuationsByActuator = new List<List<ActuationAction>>();

            foreach (var keyValuePair in actuationActionsByActuatorMap)
            {
                actuatorActuationsByActuator.Add(keyValuePair.Value);
            }

            // Get the Cartesian product of ActuationActions that don't share Actuators.
            var actuationActionByActuatorCartesianProducts = GetNaryCartesianProducts(actuatorActuationsByActuator);
            var actionByActuatorCartesianProducts = actuationActionByActuatorCartesianProducts.Select(actuationActionByActuatorCartesianProduct =>
                actuationActionByActuatorCartesianProduct.Select(actuationAction =>
                    actuationAction as Models.OntologicalModels.Action));

            // Get all possible combinations for each ActuationAction by Actuator Cartesian product.
            foreach (var actionByActuatorCartesianProduct in actionByActuatorCartesianProducts)
            {
                var actionCombinations = GetActionCombinations(actionByActuatorCartesianProduct);

                actuationActionCombinations.UnionWith(actionCombinations);
            }

            // Perform a conversion to the ActuationAction type for easier handling.
            return actuationActionCombinations.Select(actuationActionCombination =>
                actuationActionCombination.Select(actuationAction =>
                    actuationAction as ActuationAction))!;
        }

        private IEnumerable<IEnumerable<ReconfigurationAction>> GetReconfigurationActionCombinations(IEnumerable<Models.OntologicalModels.Action> actions)
        {
            // Get all possible combinations.
            var reconfigurationActionCombinations = GetActionCombinations(actions);

            // Perform a conversion to the ReconfigurationAction type for easier handling.
            return reconfigurationActionCombinations.Select(reconfigurationActionCombination =>
                reconfigurationActionCombination.Select(reconfigurationAction =>
                    reconfigurationAction as ReconfigurationAction))!;
        }

        private HashSet<HashSet<Models.OntologicalModels.Action>> GetActionCombinations(IEnumerable<Models.OntologicalModels.Action> actions)
        {
            // This method creates combinations Actions to simulate in tandem. Since there are no possibilities of encountering contradicting
            // Actions for the same property (due to validations disallowing contradicting OptimalCondition constraints), there will also be
            // no Actions with contradicting Effects.

            // Ensure that the set of sets has unique elements with the equality comparer.
            var actionCombinations = new HashSet<HashSet<Models.OntologicalModels.Action>>(new SetEqualityComparer<Models.OntologicalModels.Action>());

            foreach (var action in actions)
            {
                // Pick the current Action out of the collection.
                var remainingActions = actions.Where(innerAction => innerAction != action);

                if (!remainingActions.Any())
                {
                    // If there are no remaining Actions in the collection, we have to create the set of
                    // Actions with the current Action and add it to the set of combinations.
                    var singleActionSet = new HashSet<Models.OntologicalModels.Action>
                    {
                        action
                    };

                    actionCombinations.Add(singleActionSet);
                }
                else
                {
                    // In case of more remaining Actions, we call this method again with the remaining Action
                    // collection and add the results to the set of combinations.
                    var remainingActionCombinations = GetActionCombinations(remainingActions);
                    actionCombinations.UnionWith(remainingActionCombinations);

                    // For each Action combination from the collection of remaining Actions, create a new
                    // set and add the current Action to it to make a new combination before adding it to the
                    // set of combinations.
                    foreach (var remainingActionCombination in remainingActionCombinations)
                    {
                        var multipleActionSet = new HashSet<Models.OntologicalModels.Action>();

                        multipleActionSet.UnionWith(remainingActionCombination);
                        multipleActionSet.Add(action);

                        actionCombinations.Add(multipleActionSet);
                    }
                }
            }

            return actionCombinations;
        }

        private IEnumerable<SimulationConfiguration> GetSimulationConfigurationsFromActionCombinations(IEnumerable<IEnumerable<ActuationAction>> actuationActionCombinations,
            IEnumerable<IEnumerable<ReconfigurationAction>> reconfigurationActionCombinations,
            int simulationGranularity)
        {
            var simulationConfigurations = new List<SimulationConfiguration>();

            // Populate simulation ticks with every possible ActuationAction combination by index.
            var allSimulationTicksByIndex = new List<List<SimulationTick>>();

            for (var i = 0; i < simulationGranularity; i++)
            {
                var simulationTicksWithCurrentIndex = new List<SimulationTick>();

                foreach (var actuationActionCombination in actuationActionCombinations)
                {
                    var simulationTick = new SimulationTick
                    {
                        ActionsToExecute = actuationActionCombination,
                        TickIndex = i
                    };

                    simulationTicksWithCurrentIndex.Add(simulationTick);
                }

                allSimulationTicksByIndex.Add(simulationTicksWithCurrentIndex);
            }

            // Get all possible Cartesian pairings of simulation ticks that together form full simulation configurations.
            var simulationTickCombinations = GetNaryCartesianProducts(allSimulationTicksByIndex);

            // Get all unique Actuators from ActuationAction combinations by getting the longest combination and extracting those ActuationActions' Actuators.
            // This ensures that all Actuators that should be present are present.
            var greatestCombinationLength = 0;

            foreach (var actuationActionCombination in actuationActionCombinations)
            {
                if (greatestCombinationLength < actuationActionCombination.Count())
                {
                    greatestCombinationLength = actuationActionCombination.Count();
                }
            }

            var actuationActionCombinationLongest = actuationActionCombinations.Where(actuationActionCombination => actuationActionCombination.Count() == greatestCombinationLength)
                .First();
            var allActuators = actuationActionCombinationLongest.Select(actuationAction => actuationAction.ActuatorState.Actuator.Name);

            // Filter out simulation tick combinations where every Actuator isn't present in at least one tick per combination and construct simulation
            // configurations with the ones that pass.
            foreach (var simulationTickCombination in simulationTickCombinations)
            {
                var actuatorsPresent = new List<bool>();

                foreach (var actuatorName in allActuators)
                {
                    var actuatorPresent = false;

                    foreach (var simulationTick in simulationTickCombination)
                    {
                        foreach (var actuationAction in simulationTick.ActionsToExecute)
                        {
                            if (actuationAction.ActuatorState.Actuator.Name.Equals(actuatorName))
                            {
                                actuatorPresent = true;
                            }
                        }
                    }

                    actuatorsPresent.Add(actuatorPresent);
                }

                var allActuatorsPresent = actuatorsPresent.All(actuatorPresent => actuatorPresent == true);

                if (allActuatorsPresent)
                {
                    SimulationConfiguration simulationConfiguration = null!;

                    if (reconfigurationActionCombinations.Any())
                    {
                        foreach (var reconfigurationActionCombination in reconfigurationActionCombinations)
                        {
                            simulationConfiguration = new SimulationConfiguration
                            {
                                SimulationTicks = simulationTickCombination,
                                PostTickActions = reconfigurationActionCombination
                            };
                        }
                    }
                    else
                    {
                        simulationConfiguration = new SimulationConfiguration
                        {
                            SimulationTicks = simulationTickCombination,
                            PostTickActions = []
                        };
                    }

                    simulationConfigurations.Add(simulationConfiguration);
                }
            }

            return simulationConfigurations;
        }

        private HashSet<HashSet<T>> GetNaryCartesianProducts<T>(IEnumerable<IEnumerable<T>> originalCollectionOfCollections)
        {
            var combinations = new HashSet<HashSet<T>>(new SetEqualityComparer<T>());

            foreach (var currentCollection in originalCollectionOfCollections)
            {
                var collectionOfRemainingCollections = originalCollectionOfCollections.Where(collection => collection != currentCollection);

                foreach (var element in currentCollection)
                {
                    if (!collectionOfRemainingCollections.Any())
                    {
                        var singleElementCombination = new HashSet<T>()
                        {
                            element
                        };

                        combinations.Add(singleElementCombination);
                    }
                    else
                    {
                        var remainingCombinations = GetNaryCartesianProducts(collectionOfRemainingCollections);

                        foreach (var remainingCombination in remainingCombinations)
                        {
                            remainingCombination.Add(element);
                        }

                        combinations.UnionWith(remainingCombinations);
                    }
                }
            }

            return combinations;
        }

        //private IEnumerable<SimulationResult> Simulate(IEnumerable<SimulationConfiguration> simulationConfigurations,
        //    IEnumerable<OptimalCondition> optimalConditions,
        //    PropertyCache propertyCache)
        //{
        //    var simulationResults = new List<SimulationResult>();

        //    foreach (var actionCombination in actionCombinations)
        //    {
        //        var actuationActions = actionCombination.Where(action => action is ActuationAction)
        //            .Select(action => (ActuationAction)action);
        //        var reconfigurationActions = actionCombination.Where(action => action is ReconfigurationAction)
        //            .Select(action => (ReconfigurationAction)action);

        //        // Make a deep copy of the property cache for simulations.
        //        var propertyCacheCopy = new PropertyCache
        //        {
        //            Properties = new Dictionary<string, Property>(),
        //            ConfigurableParameters = new Dictionary<string, ConfigurableParameter>()
        //        };

        //        foreach (var keyValuePair in propertyCache.Properties)
        //        {
        //            propertyCacheCopy.Properties.Add(keyValuePair.Key, new Property
        //            {
        //                Name = keyValuePair.Value.Name,
        //                OwlType = keyValuePair.Value.OwlType,
        //                Value = keyValuePair.Value.Value
        //            });
        //        }

        //        foreach (var keyValuePair in propertyCache.ConfigurableParameters)
        //        {
        //            propertyCacheCopy.Properties.Add(keyValuePair.Key, new ConfigurableParameter
        //            {
        //                Name = keyValuePair.Value.Name,
        //                OwlType = keyValuePair.Value.OwlType,
        //                Value = keyValuePair.Value.Value,
        //                LowerLimitValue = keyValuePair.Value.LowerLimitValue,
        //                UpperLimitValue = keyValuePair.Value.UpperLimitValue
        //            });
        //        }

        //        // Simulate each ActuationAction and update the property cache copy.
        //        foreach (var actuationAction in actuationActions)
        //        {
        //            var inputs = new Dictionary<string, object>
        //            {
        //                { "actuator", actuationAction.ActuatorState.Actuator },
        //                { "actuatorState", actuationAction.ActuatorState.Name }
        //            };

        //            //var outputs = GetOutputsFromSimulation(actuationAction.ActuatorState.Actuator.Model, inputs);
        //        }

        //        // Simulate each ReconfigurationAction and update the property cache copy.
        //        foreach (var reconfigurationAction in reconfigurationActions)
        //        {
                    
        //        }

        //        // Check that every OptimalCondition passes with respect to the values in the property cache copy.
        //        foreach (var optimalCondition in optimalConditions)
        //        {

        //        }

        //        // based on the optimalcondition check, make a simulationresult and add it to the collection
        //    }

        //    // 1. run simulations for all actions present
        //    // this should be done with some heuristics. spawn them all but don't run them all for the entire
        //    // duration of the simulation (e.g., 1h). cut this up into smaller, granular chunks, and then
        //    // continue with those that seem to get closer. we could outsource this deciding logic via a
        //    // user-defined delegate
        //    // 2. check the results of all remaining simulations at the end of the whole duration (e.g., 1h) and
        //    // pick those whose results meet their respective optimalconditions (and don't break other constraints)

        //    return new List<SimulationResult>();
        //}

        //private IDictionary<string, object> GetOutputsFromSimulation(string fmuFilePath, IDictionary<string, object> inputs)
        //{
        //    // TODO:
        //    // simulate all combinations of reconfigurationactions
        //    // to ensure a finite number of simulations, a granularity factor can be used on the parameters to simulate
        //    // we can find the number of simulations for each configurableproperty by taking its min - max range and dividing
        //    // by the granularity factor
        //    // this is one type of hard - coded logic, however, we should make it possible to delegate this logic to the user

        //    // simulate the actuations first since reconfigurations use measured properties as inputs
        //    // use granularity for simulating


        //}
    }
}
