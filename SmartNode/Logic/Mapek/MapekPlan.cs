using J2N.Text;
using Logic.FactoryInterface;
using Logic.Mapek.EqualityComparers;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Text;
using VDS.RDF;
using VDS.RDF.Query;

namespace Logic.Mapek
{
    internal class MapekPlan : IMapekPlan
    {
        private readonly ILogger<MapekPlan> _logger;
        private readonly IFactory _factory;

        public MapekPlan(IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetRequiredService<ILogger<MapekPlan>>();
            _factory = serviceProvider.GetRequiredService<IFactory>();
        }

        public SimulationConfiguration Plan(IEnumerable<OptimalCondition> optimalConditions,
            IEnumerable<Models.OntologicalModels.Action> actions,
            PropertyCache propertyCache,
            IGraph instanceModel)
        {
            _logger.LogInformation("Starting the Plan phase.");

            // Set some granularity values for different types Action simulations.
            var actuationSimulationGranularity = 4;
            var reconfigurationSimulationGranularity = 7;

            var plannedActions = new List<Models.OntologicalModels.Action>();

            // The two Action types should be split to facilitate simulations. ActuationActions may be de/activated at any point during
            // the available time to restore an OptimalCondition. On the other hand, ReconfigurationActions can't necessarily be dependent
            // on a time factor since the underlying soft Sensor algorithms may take long to run. However, they will nonetheless be included
            // in simulation configurations to ensure that all OptimalConditions are met when simulating different types of Actions in
            // conjunction. To avoid generating duplicate combinations for simulation ticks (intervals), the removal of ReconfigurationActions
            // should be done before by splitting the two types of Actions and generating combinations for each separately.
            var actuationActions = actions.Where(action => action is ActuationAction)
                .Select(action => action as ActuationAction);
            var reconfigurationActions = actions.Where(action => action is ReconfigurationAction)
                .Select(action => action as ReconfigurationAction);

            _logger.LogInformation("Getting Action combinations.");

            // Get all possible combinations for ActuationActions.
            var actuationActionCombinations = GetActuationActionCombinations(actuationActions!);

            // Get all possible combinations for ReconfigurationActions.
            var reconfigurationActionCombinations = GetReconfigurationActionCombinations(reconfigurationActions!, reconfigurationSimulationGranularity);

            _logger.LogInformation("Generating simulation configurations.");

            // Get all possible simulation configurations for the given Actions.
            var simulationConfigurations = GetSimulationConfigurationsFromActionCombinations(actuationActionCombinations,
                reconfigurationActionCombinations,
                actuationSimulationGranularity);

            _logger.LogInformation("Generated a total of {total} simulation configurations.", simulationConfigurations.Count());

            var successfulConfigurations = Simulate(simulationConfigurations, optimalConditions, propertyCache, instanceModel, actuationSimulationGranularity);

            if (!successfulConfigurations.Any())
            {
                _logger.LogWarning("No successful simulation configurations found. This indicates that no Actions the system can take will restore " +
                    "OptimalConditions. Consider adding additional Actuators or setting up soft-Sensors with different ConfigurableParameters.");

                return new SimulationConfiguration
                {
                    PostTickActions = [],
                    SimulationTicks = []
                };
            }

            _logger.LogInformation("Found a total of {total} successful configurations.", successfulConfigurations.Count());

            var optimalConfiguration = GetOptimalConfiguration(instanceModel, propertyCache, successfulConfigurations);

            return optimalConfiguration;
        }

        private IEnumerable<IEnumerable<ActuationAction>> GetActuationActionCombinations(IEnumerable<ActuationAction> actuationActions)
        {
            var actuationActionCombinations = new HashSet<HashSet<Models.OntologicalModels.Action>>(new ActionSetEqualityComparer());

            // Group ActuationActions by their Actuators to allow creating combinations that don't consist of different states of the same Actuator.
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

        private IEnumerable<IEnumerable<ReconfigurationAction>> GetReconfigurationActionCombinations(IEnumerable<ReconfigurationAction> reconfigurationActions,
            int simulationGranularity)
        {
            var reconfigurationActionCombinations = new HashSet<HashSet<Models.OntologicalModels.Action>>(new ActionSetEqualityComparer());

            // Create ReconfigurationActions with new values to set for their respective ConfigurableParameters.
            var reconfigurationActionsWithValues = new List<ReconfigurationAction>();

            foreach (var reconfigurationAction in reconfigurationActions)
            {
                var valueHandler = _factory.GetValueHandlerImplementation(reconfigurationAction.ConfigurableParameter.OwlType);
                var possibleValues = valueHandler.GetPossibleValuesForReconfigurationAction(reconfigurationAction.ConfigurableParameter.Value,
                    reconfigurationAction.ConfigurableParameter.LowerLimitValue,
                    reconfigurationAction.ConfigurableParameter.UpperLimitValue,
                    simulationGranularity,
                    reconfigurationAction.Effect,
                    reconfigurationAction.ConfigurableParameter.Name);

                foreach (var possibleValue in possibleValues)
                {
                    var reconfigurationActionWithValue = new ReconfigurationAction
                    {
                        ConfigurableParameter = reconfigurationAction.ConfigurableParameter,
                        Effect = reconfigurationAction.Effect,
                        Name = reconfigurationAction.Name,
                        NewParameterValue = possibleValue
                    };

                    reconfigurationActionsWithValues.Add(reconfigurationActionWithValue);
                }
            }

            // Group ReconfigurationActions by their ConfigurableParameter to allow creating combinations that don't consist of different reconfigurations of the same Property.
            var reconfigurationActionsByConfigurableParameterMap = new Dictionary<string, List<ReconfigurationAction>>();

            foreach (var reconfigurationAction in reconfigurationActionsWithValues)
            {
                if (reconfigurationActionsByConfigurableParameterMap.TryGetValue(reconfigurationAction.ConfigurableParameter.Name, out List<ReconfigurationAction> reconfigurationActionsInMap))
                {
                    reconfigurationActionsInMap.Add(reconfigurationAction);
                }
                else
                {
                    reconfigurationActionsByConfigurableParameterMap.Add(reconfigurationAction.ConfigurableParameter.Name, new List<ReconfigurationAction>
                    {
                        reconfigurationAction
                    });
                }
            }

            // Convert to a simple collection.
            var reconfigurationActionsByConfigurableParameter = new List<List<ReconfigurationAction>>();

            foreach (var keyValuePair in reconfigurationActionsByConfigurableParameterMap)
            {
                reconfigurationActionsByConfigurableParameter.Add(keyValuePair.Value);
            }

            // Get the Cartesian product of ReconfigurationActions that don't share ConfigurableParameters.
            var reconfigurationActionByConfigurableParameterCartesianProducts = GetNaryCartesianProducts(reconfigurationActionsByConfigurableParameter);
            var actionByConfigurableParameterCartesianProducts = reconfigurationActionByConfigurableParameterCartesianProducts.Select(reconfigurationActionByConfigurableParameterCartesianProduct =>
                reconfigurationActionByConfigurableParameterCartesianProduct.Select(reconfigurationAction =>
                    reconfigurationAction as Models.OntologicalModels.Action));

            // Get all possible combinations for each ReconfigurationAction by ConfigurableParameter Cartesian product.
            foreach (var actionByConfigurableParameterCartesianProduct in actionByConfigurableParameterCartesianProducts)
            {
                var actionCombinations = GetActionCombinations(actionByConfigurableParameterCartesianProduct);

                reconfigurationActionCombinations.UnionWith(actionCombinations);
            }

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

            if (actuationActionCombinations.Any())
            {
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
                // This ensures that all Actuators that should be present in a simulation are present.
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
                // configurations with the combinations that pass.
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
                                    SimulationTicks = simulationTickCombination.Reverse(), // Must be reversed due to how the combinations are constructed.
                                    PostTickActions = reconfigurationActionCombination
                                };
                            }
                        }
                        else
                        {
                            simulationConfiguration = new SimulationConfiguration
                            {
                                SimulationTicks = simulationTickCombination.Reverse(), // Must be reversed due to how the combinations are constructed.
                                PostTickActions = []
                            };
                        }

                        simulationConfigurations.Add(simulationConfiguration);
                    }
                }
            }
            else
            {
                // In case of no ActuationActions present, simply construct simulation configurations from all combinations of ReconfigurationActions.
                foreach (var reconfigurationActionCombination in reconfigurationActionCombinations)
                {
                    var simulationConfiguration = new SimulationConfiguration
                    {
                        SimulationTicks = [],
                        PostTickActions = reconfigurationActionCombination
                    };

                    simulationConfigurations.Add(simulationConfiguration);
                }
            }
            
            return simulationConfigurations;
        }

        private HashSet<HashSet<T>> GetNaryCartesianProducts<T>(IEnumerable<IEnumerable<T>> originalCollectionOfCollections)
        {
            // This method gets the n-ary Cartesian product of multiple collections.
            var combinations = new HashSet<HashSet<T>>(new SetEqualityComparer<T>());

            foreach (var currentCollection in originalCollectionOfCollections)
            {
                // Get all remaining collections.
                var collectionOfRemainingCollections = originalCollectionOfCollections.Where(collection => collection != currentCollection);

                foreach (var element in currentCollection)
                {
                    if (!collectionOfRemainingCollections.Any())
                    {
                        // If there are no remaining collections, simply make a set of the current element.
                        var singleElementCombination = new HashSet<T>()
                        {
                            element
                        };

                        combinations.Add(singleElementCombination);
                    }
                    else
                    {
                        // If there are remaining collections, get their n-ary Cartesian product and add the current element to all sets returned.
                        var remainingCombinations = GetNaryCartesianProducts(collectionOfRemainingCollections);

                        foreach (var remainingCombination in remainingCombinations)
                        {
                            remainingCombination.Add(element);
                        }

                        // Add the remaining n-ary Cartesian product to the set of sets.
                        combinations.UnionWith(remainingCombinations);
                    }
                }
            }

            return combinations;
        }

        private IEnumerable<SimulationConfiguration> Simulate(IEnumerable<SimulationConfiguration> simulationConfigurations,
            IEnumerable<OptimalCondition> optimalConditions,
            PropertyCache propertyCache,
            IGraph instanceModel,
            int simulationGranularity)
        {
            var successfulSimulationConfigurations = new List<SimulationConfiguration>();

            // Retrieve the host platform FMU for ActuationAction simulations.
            var fmuFilePath = GetHostPlatformFmu(instanceModel, simulationConfigurations.First());

            // Get the unsatisfied OptimalCondition with the lowest mitigation time to use it as the simulation's maximum time.
            var unsatisfiedOptimalConditions = optimalConditions.Where(optimalCondition => optimalCondition.UnsatisfiedAtomicConstraints.Any());
            var maximumSimulationTime = GetMaximumSimulationTime(unsatisfiedOptimalConditions);

            foreach (var simulationConfiguration in simulationConfigurations)
            {
                // Make a deep copy of the property cache for simulations.
                var propertyCacheCopy = GetPropertyCacheCopy(propertyCache);

                // Run the simulation by executing ActuationActions in their respective simulation ticks followed by ReconfigurationActions.
                foreach (var simulationTick in simulationConfiguration.SimulationTicks)
                {
                    var timeInterval = maximumSimulationTime / simulationGranularity;
                    var simulationTime = (simulationTick.TickIndex + 1) * timeInterval;

                    var fmuActuationInputs = new Dictionary<string, object>();

                    foreach (var actuationAction in simulationTick.ActionsToExecute)
                    {
                        // Shave off the long name URIs from the instance model.
                        var simpleActuatorName = MapekUtilities.GetSimpleName(actuationAction.ActuatorState.Actuator.Name);
                        var simpleActuatorStateName = MapekUtilities.GetSimpleName(actuationAction.ActuatorState.Name);

                        fmuActuationInputs.Add(simpleActuatorName + "_state", simpleActuatorStateName);
                    }

                    var propertyKeyValuePairs = ExecuteFmu(fmuFilePath, fmuActuationInputs, simulationTime);

                    AssignPropertyCacheCopyValues(propertyCacheCopy, propertyKeyValuePairs);
                }

                foreach (var reconfigurationAction in simulationConfiguration.PostTickActions)
                {
                    var fmuReconfigurationInputs = new Dictionary<string, object>();

                    // Shave off the long name URIs from the instance model.
                    var simpleConfigurableParameterName = MapekUtilities.GetSimpleName(reconfigurationAction.ConfigurableParameter.Name);

                    fmuReconfigurationInputs.Add(simpleConfigurableParameterName, reconfigurationAction.NewParameterValue);

                    // Get FMUs of all soft-sensors that take the current ConfigurableParameter as an Input Property.
                    var softSensorFmus = GetSoftSensorFmuFilePathsFromConfigurableParameterName(instanceModel, reconfigurationAction.ConfigurableParameter.Name);

                    // Execute all FMUs and adjust the property cache accordingly.
                    foreach (var softSensorFmu in softSensorFmus)
                    {
                        var propertyKeyValuePairs = ExecuteFmu(fmuFilePath, fmuReconfigurationInputs);

                        AssignPropertyCacheCopyValues(propertyCacheCopy, propertyKeyValuePairs);
                    }
                }

                // Check that every OptimalCondition passes with respect to the values in the property cache copy.
                var allOptimalConditionsSatisfied = AreAllOptimalConditionsSatisfied(optimalConditions, propertyCacheCopy);

                if (allOptimalConditionsSatisfied)
                {
                    simulationConfiguration.ResultingPropertyCache = propertyCacheCopy;

                    successfulSimulationConfigurations.Add(simulationConfiguration);
                }
            }

            return successfulSimulationConfigurations;
        }

        private string GetHostPlatformFmu(IGraph instanceModel, SimulationConfiguration simulationConfiguration)
        {
            if (!simulationConfiguration.SimulationTicks.Any())
            {
                return string.Empty;
            }

            // Retrieve all Actuators to be used in the simulations and ensure that they belong to the same host Platform such that the Platform's
            // FMU will contain all of their relevant input/output variables.
            var actuatorNames = new HashSet<string>();
            var clauseBuilder = new StringBuilder();

            foreach (var simulationTick in simulationConfiguration.SimulationTicks)
            {
                foreach (var actuationAction in simulationTick.ActionsToExecute)
                {
                    if (!actuatorNames.Contains(actuationAction.ActuatorState.Actuator.Name))
                    {
                        actuatorNames.Add(actuationAction.ActuatorState.Actuator.Name);

                        // Add the Actuator name to the query filter.
                        clauseBuilder.AppendLine("?platform sosa:hosts <" + actuationAction.ActuatorState.Actuator.Name + "> .");
                    }
                }
            }

            var query = MapekUtilities.GetParameterizedStringQuery();

            var clause = clauseBuilder.ToString();

            query.CommandText = @"SELECT ?fmuFilePath WHERE {
                ?platform rdf:type sosa:Platform . " +
                clause +
                "?platform meta:hasModel ?fmuFilePath . }";

            var queryResult = (SparqlResultSet)instanceModel.ExecuteQuery(query);

            // There can theoretically be multiple Platforms hosting the same Actuator, but we limit ourselves to expect a single Platform
            // per instance model. There should therefore be only one result.
            return queryResult.Results[0]["fmuFilePath"].ToString().Split('^')[0];
        }

        private PropertyCache GetPropertyCacheCopy(PropertyCache originalPropertyCache)
        {
            var propertyCacheCopy = new PropertyCache
            {
                Properties = new Dictionary<string, Property>(),
                ConfigurableParameters = new Dictionary<string, ConfigurableParameter>()
            };

            foreach (var keyValuePair in originalPropertyCache.Properties)
            {
                propertyCacheCopy.Properties.Add(keyValuePair.Key, new Property
                {
                    Name = keyValuePair.Value.Name,
                    OwlType = keyValuePair.Value.OwlType,
                    Value = keyValuePair.Value.Value
                });
            }

            foreach (var keyValuePair in originalPropertyCache.ConfigurableParameters)
            {
                propertyCacheCopy.Properties.Add(keyValuePair.Key, new ConfigurableParameter
                {
                    Name = keyValuePair.Value.Name,
                    OwlType = keyValuePair.Value.OwlType,
                    Value = keyValuePair.Value.Value,
                    LowerLimitValue = keyValuePair.Value.LowerLimitValue,
                    UpperLimitValue = keyValuePair.Value.UpperLimitValue
                });
            }

            return propertyCacheCopy;
        }

        private int GetMaximumSimulationTime(IEnumerable<OptimalCondition> optimalConditions)
        {
            var maximumSimulationTime = int.MaxValue;

            foreach (var optimalCondition in optimalConditions)
            {
                if (optimalCondition.ReachedInMaximumSeconds < maximumSimulationTime)
                {
                    maximumSimulationTime = optimalCondition.ReachedInMaximumSeconds;
                }
            }

            return maximumSimulationTime;
        }

        private IDictionary<string, object> ExecuteFmu(string fmuFilePath, IDictionary<string, object> inputs, double timeValue = -1)
        {
            // TODO: make reading from and writing to fmus work!
            return new Dictionary<string, object>();
        }

        private void AssignPropertyCacheCopyValues(PropertyCache propertyCacheCopy, IDictionary<string, object> fmuOutputs)
        {
            // Find the correct Property from the simpler output variable name and assign its value.
            foreach (var fmuOutput in fmuOutputs)
            {
                foreach (var property in propertyCacheCopy.Properties.Keys)
                {
                    if (property.EndsWith(fmuOutput.Key))
                    {
                        propertyCacheCopy.Properties[property].Value = fmuOutput.Value;
                    }
                }
            }
        }

        private IEnumerable<string> GetSoftSensorFmuFilePathsFromConfigurableParameterName(IGraph instanceModel, string configurableParameterName)
        {
            var fmuFilePaths = new List<string>();

            var query = MapekUtilities.GetParameterizedStringQuery();

            query.CommandText = @"SELECT ?fmuFilePath WHERE {
                @inputParameter rdf:type meta:ConfigurableParameter .
                ?procedure rdf:type sosa:Procedure .
                ?procedure ssn:hasInput @inputParameter .
                ?procedure meta:hasModel ?fmuFilePath . }";

            query.SetUri("inputParameter", new Uri(configurableParameterName));

            var queryResult = (SparqlResultSet)instanceModel.ExecuteQuery(query);

            foreach (var result in queryResult.Results)
            {
                var fmuFilePath = result["fmuFilePath"].ToString().Split('^')[0];

                fmuFilePaths.Add(fmuFilePath);
            }

            return fmuFilePaths;
        }

        private bool AreAllOptimalConditionsSatisfied(IEnumerable<OptimalCondition> optimalConditions, PropertyCache propertyCache)
        {
            foreach (var optimalCondition in optimalConditions)
            {
                var valueHandler = _factory.GetValueHandlerImplementation(optimalCondition.ConstraintValueType);

                object propertyValue;

                if (propertyCache.ConfigurableParameters.TryGetValue(optimalCondition.Property, out ConfigurableParameter configurableParameter))
                {
                    propertyValue = configurableParameter.Value;
                }
                else if (propertyCache.Properties.TryGetValue(optimalCondition.Property, out Property property))
                {
                    propertyValue = property.Value;
                }
                else
                {
                    throw new Exception($"Property {optimalCondition.Property} was not found in the system.");
                }

                foreach (var constraint in optimalCondition.Constraints)
                {
                    var unsatisfiedConstraints = valueHandler.GetUnsatisfiedConstraintsFromEvaluation(constraint, propertyValue);

                    if (unsatisfiedConstraints.Any())
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private SimulationConfiguration GetOptimalConfiguration(IGraph instanceModel,
            PropertyCache propertyCache,
            IEnumerable<SimulationConfiguration> successfulSimulationConfigurations)
        {
            // This method finds the optimal configuration out of the collection of successful simulation configurations. It does so by first
            // finding a subset that contains the highest number of optimized Properties. For simplicity, the value optimized by is not checked
            // since deciding on the 'worth' of each Property's amount with respect to another is pure domain knowledge and could thus be
            // out-sourced as custom logic to the user. In case of multiple configurations remaining after the first filter, a further subset
            // is picked consisting of configurations with the lowest total number of Actions to take. In case of multiple configurations still
            // remaining, the first one is returned.

            var propertyChangesToOptimizeFor = GetPropertyChangesToOptimizeFor(instanceModel, propertyCache);

            // Keep count of the maximum number of optimized Properties for caching the configurations with the greatest numbers of optimized
            // Properties.
            var currentMaximumOptimizedProperties = 0;
            var configurationsWithOptimizedProperties = new List<SimulationConfiguration>();

            foreach (var successfulSimulationConfiguration in successfulSimulationConfigurations)
            {
                var optimizedProperties = 0;

                foreach (var propertyChangeToOptimizeFor in propertyChangesToOptimizeFor)
                {
                    Property propertyToCompare = null!;

                    bool propertyFound = propertyCache.Properties.TryGetValue(propertyChangeToOptimizeFor.Property.Name, out propertyToCompare);

                    if (!propertyFound)
                    {
                        // If the first cache doesn't contain the Property, it must be in the second one.
                        propertyToCompare = propertyCache.ConfigurableParameters[propertyChangeToOptimizeFor.Property.Name];
                    }

                    var valueHandler = _factory.GetValueHandlerImplementation(propertyToCompare.OwlType);

                    var isOptimized = false;

                    if (propertyChangeToOptimizeFor.OptimizeFor == Effect.ValueIncrease)
                    {
                        isOptimized = valueHandler.IsGreaterThan(propertyToCompare.Value, propertyChangeToOptimizeFor.Property.Value);
                    }
                    else
                    {
                        isOptimized = valueHandler.IsLessThan(propertyToCompare.Value, propertyChangeToOptimizeFor.Property.Value);
                    }

                    if (isOptimized)
                    {
                        optimizedProperties++;
                    }
                }

                if (optimizedProperties > currentMaximumOptimizedProperties)
                {
                    // If there are more optimized Properties than the current maximum, create a new collection.
                    currentMaximumOptimizedProperties = optimizedProperties;
                    configurationsWithOptimizedProperties = new List<SimulationConfiguration>
                    {
                        successfulSimulationConfiguration
                    };
                }
                else if (optimizedProperties == currentMaximumOptimizedProperties)
                {
                    // Otherwise, if there is a matching number of optimized Properties, simply add the current configuration to the collection.
                    configurationsWithOptimizedProperties.Add(successfulSimulationConfiguration);
                }
            }

            // Keep count of the lowest number of Actions per simulation configuration to cache the most 'efficient' ones.
            var lowestNumberOfActions = int.MaxValue;
            var configurationsWithLowestActions = new List<SimulationConfiguration>();

            foreach (var configurationWithOptimizedProperties in configurationsWithOptimizedProperties)
            {
                var numberOfActionsToTake = 0;

                foreach (var simulationTick in configurationWithOptimizedProperties.SimulationTicks)
                {
                    numberOfActionsToTake += simulationTick.ActionsToExecute.Count();
                }

                numberOfActionsToTake += configurationWithOptimizedProperties.PostTickActions.Count();

                if (numberOfActionsToTake < lowestNumberOfActions)
                {
                    lowestNumberOfActions = numberOfActionsToTake;
                    configurationsWithLowestActions = new List<SimulationConfiguration>
                    {
                        configurationWithOptimizedProperties
                    };
                }
                else if (numberOfActionsToTake == lowestNumberOfActions)
                {
                    configurationsWithLowestActions.Add(configurationWithOptimizedProperties);
                }
            }

            // In case there are still multiple Actions, simply return the first one.
            return configurationsWithLowestActions[0];
        }

        private IEnumerable<PropertyChange> GetPropertyChangesToOptimizeFor(IGraph instanceModel, PropertyCache propertyCache)
        {
            var propertyChangesToOptimizeFor = new List<PropertyChange>();

            var query = MapekUtilities.GetParameterizedStringQuery();

            query.CommandText = @"SELECT ?propertyChange ?property ?effect WHERE {
                ?platform rdf:type sosa:Platform .
                ?platform meta:optimizesFor ?propertyChange .
                ?propertyChange ssn:forProperty ?property .
                ?propertyChange meta:affectsPropertyWith ?effect . }";

            var queryResult = (SparqlResultSet)instanceModel.ExecuteQuery(query);

            foreach (var result in queryResult.Results)
            {
                var propertyChangeName = result["propertyChange"].ToString();
                var propertyName = result["property"].ToString();
                var effectName = result["effect"].ToString().Split("/")[^1];

                Property property = null!;

                var propertyFound = propertyCache.Properties.TryGetValue(propertyName, out property!);

                // Check where in the property cache the Property is.
                if (!propertyFound)
                {
                    var configurableParameterFound = propertyCache.ConfigurableParameters.TryGetValue(propertyName, out ConfigurableParameter? configurableParameter);

                    if (!configurableParameterFound)
                    {
                        throw new Exception($"Property {property} was not found in the Property cache.");
                    }
                    else
                    {
                        property = configurableParameter!;
                    }
                }

                if (!Enum.TryParse(effectName, out Effect effect))
                {
                    throw new Exception($"Enum value {effectName} is not supported.");
                }

                var propertyChange = new PropertyChange
                {
                    Name = propertyChangeName,
                    Property = property,
                    OptimizeFor = effect
                };

                propertyChangesToOptimizeFor.Add(propertyChange);
            }

            return propertyChangesToOptimizeFor;
        }
    }
}
