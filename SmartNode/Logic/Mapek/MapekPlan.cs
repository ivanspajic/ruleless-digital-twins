using Femyou;
using Logic.FactoryInterface;
using Logic.Mapek.Comparers;
using Logic.Mapek.EqualityComparers;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Logic.ValueHandlerInterfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using VDS.Common.Collections.Enumerations;
using VDS.RDF;

namespace Logic.Mapek
{
    internal class MapekPlan : IMapekPlan
    {
	public static Dictionary<string, IModel> fmuDict = new Dictionary<string, IModel>();
	public static Dictionary<string, IInstance> iDict = new Dictionary<string, IInstance>();
	private static int iCount = 0;

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
            IGraph instanceModel,
            string fmuDirectory,
            int actuationSimulationGranularity)
        {
            _logger.LogInformation("Starting the Plan phase.");

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
            var reconfigurationActionCombinations = GetReconfigurationActionCombinations(reconfigurationActions!);

            _logger.LogInformation("Generating simulation configurations.");

            // Get all possible simulation configurations for the given Actions.
            var simulationConfigurations = GetSimulationConfigurationsFromActionCombinations(actuationActionCombinations,
                reconfigurationActionCombinations,
                optimalConditions,
                actuationSimulationGranularity);

            _logger.LogInformation("Generated a total of {total} simulation configurations.", simulationConfigurations.Count());

            // Execute the simulations and obtain their results.
            Simulate(simulationConfigurations, instanceModel, propertyCache, fmuDirectory);

            // Find the optimal simulation configuration.
            var optimalConfiguration = GetOptimalConfiguration(instanceModel, propertyCache, optimalConditions, simulationConfigurations);

            LogOptimalSimulationConfiguration(optimalConfiguration);

            return optimalConfiguration;
        }

        private IEnumerable<IEnumerable<ActuationAction>> GetActuationActionCombinations(IEnumerable<ActuationAction> actuationActions)
        {
            var actuationActionCombinations = new HashSet<HashSet<Models.OntologicalModels.Action>>(new ActionSetEqualityComparer());

            // Group ActuationActions by their Actuators to allow creating combinations that don't consist of different states of the same Actuator.
            var actuationActionsByActuatorMap = new Dictionary<string, List<ActuationAction>>();

            foreach (var actuationAction in actuationActions)
            {
                if (actuationActionsByActuatorMap.TryGetValue(actuationAction.Actuator.Name, out List<ActuationAction>? actuationActionsInMap))
                {
                    actuationActionsInMap.Add(actuationAction);
                }
                else
                {
                    actuationActionsByActuatorMap.Add(actuationAction.Actuator.Name, new List<ActuationAction>
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

        private IEnumerable<IEnumerable<ReconfigurationAction>> GetReconfigurationActionCombinations(IEnumerable<ReconfigurationAction> reconfigurationActions)
        {
            var reconfigurationActionCombinations = new HashSet<HashSet<Models.OntologicalModels.Action>>(new ActionSetEqualityComparer());

            // Group ReconfigurationActions by their ConfigurableParameter to allow creating combinations that don't consist of different reconfigurations of the same Property.
            var reconfigurationActionsByConfigurableParameterMap = new Dictionary<string, List<ReconfigurationAction>>();

            foreach (var reconfigurationAction in reconfigurationActions)
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
                    // If there are no remaining Actions in the collection, we have to create the set of Actions with the current Action and add it
                    // to the set of combinations. Additionally, to allow for empty simulations with no Actions, we also have to add the empty set.
                    var zeroActionSet = new HashSet<Models.OntologicalModels.Action>();
                    var singleActionSet = new HashSet<Models.OntologicalModels.Action>
                    {
                        action
                    };

                    actionCombinations.Add(zeroActionSet);
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
            IEnumerable<OptimalCondition> optimalConditions,
            int simulationGranularity)
        {
            var simulationConfigurations = new List<SimulationConfiguration>();

            // Get the unsatisfied OptimalCondition with the lowest mitigation time to use it as the simulation's maximum time.
            var unsatisfiedOptimalConditions = optimalConditions.Where(optimalCondition => optimalCondition.UnsatisfiedAtomicConstraints.Any());
            var maximumSimulationTime = GetMaximumSimulationTime(unsatisfiedOptimalConditions);

            if (actuationActionCombinations.Any())
            {
                // Populate simulation ticks with every possible ActuationAction combination by index.
                var allSimulationTicksByIndex = new List<List<SimulationTick>>();

                for (var i = 0; i < simulationGranularity; i++)
                {
                    var simulationTicksWithCurrentIndex = new List<SimulationTick>();

                    foreach (var actuationActionCombination in actuationActionCombinations)
                    {
                        var timeInterval = maximumSimulationTime / simulationGranularity;

                        var simulationTick = new SimulationTick
                        {
                            ActionsToExecute = actuationActionCombination,
                            TickIndex = i,
                            TickDurationSeconds = timeInterval
                        };

                        simulationTicksWithCurrentIndex.Add(simulationTick);
                    }

                    allSimulationTicksByIndex.Add(simulationTicksWithCurrentIndex);
                }

                // Get all possible Cartesian pairings of simulation ticks that together form full simulation configurations.
                var simulationTickCombinations = GetNaryCartesianProducts(allSimulationTicksByIndex);

                foreach (var simulationTickCombination in simulationTickCombinations)
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

        private void Simulate(IEnumerable<SimulationConfiguration> simulationConfigurations, IGraph instanceModel, PropertyCache propertyCache, string fmuDirectory)
        {
            // Retrieve the host platform FMU for ActuationAction simulations.
            var fmuFilePath = GetHostPlatformFmu(instanceModel, simulationConfigurations.First(), fmuDirectory);

            int i = 0;
            foreach (var simulationConfiguration in simulationConfigurations)
            {
                _logger.LogInformation("Running simulation #{run}", i++);

                // Make a deep copy of the property cache for the current simulation configuration.
                var propertyCacheCopy = GetPropertyCacheCopy(propertyCache);

                if (simulationConfiguration.SimulationTicks.Any())
                {
                    // Comment this back in for FMU testing. The Femyou library can seemingly not dispose of resources from FMUs other than Modelica reference ones.
                    // All other logic of writing inputs and reading outputs works.
                    ExecuteActuationActionFmu(fmuFilePath, simulationConfiguration, instanceModel, propertyCacheCopy);
                }

                if (simulationConfiguration.PostTickActions.Any())
                {
                    // Executing/simulating soft sensors during the Plan phase is not yet supported.
                }

                // Assign the final Property values to the results of the simulation configuration.
                simulationConfiguration.ResultingPropertyCache = propertyCacheCopy;
            }
        }

        private string GetHostPlatformFmu(IGraph instanceModel, SimulationConfiguration simulationConfiguration, string fmuDirectory)
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
                    if (!actuatorNames.Contains(actuationAction.Actuator.Name))
                    {
                        actuatorNames.Add(actuationAction.Actuator.Name);
                        
                        // Add the Actuator name to the query filter.
                        clauseBuilder.AppendLine("?platform sosa:hosts <" + actuationAction.Actuator.Name + "> .");
                    }
                }
            }

            var query = MapekUtilities.GetParameterizedStringQuery();

            var clause = clauseBuilder.ToString();

            query.CommandText = @"SELECT ?fmuFilePath WHERE {
                ?platform rdf:type sosa:Platform . " +
                clause +
                "?platform meta:hasModel ?fmuFilePath . }";

            var queryResult = instanceModel.ExecuteQuery(query, _logger);

            // There can theoretically be multiple Platforms hosting the same Actuator, but we limit ourselves to expect a single Platform
            // per instance model. There should therefore be only one result.
            var fmu = queryResult.Results[0]["fmuFilePath"].ToString().Split('^')[0];

            return Path.Combine(fmuDirectory, fmu);
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
                    Value = keyValuePair.Value.Value
                });
            }

            return propertyCacheCopy;
        }

        private IEnumerable<Property> GetObservablePropertiesFromPropertyCache(IGraph instanceModel, PropertyCache propertyCache)
        {
            var observableProperties = new List<Property>();

            var query = MapekUtilities.GetParameterizedStringQuery();

            query.CommandText = @"SELECT DISTINCT ?observableProperty WHERE {
                ?sensor rdf:type sosa:Sensor .
                ?sensor sosa:observes ?observableProperty . }";

            var queryResult = instanceModel.ExecuteQuery(query, _logger);

            foreach (var result in queryResult.Results)
            {
                var propertyName = result["observableProperty"].ToString();

                if (propertyCache.Properties.TryGetValue(propertyName, out Property property))
                {
                    observableProperties.Add(property);
                }
                else
                {
                    throw new Exception($"ObservableProperty {propertyName} was not in the cache.");
                }
            }

            return observableProperties;
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

        private void ExecuteActuationActionFmu(string fmuFilePath, SimulationConfiguration simulationConfiguration, IGraph instanceModel, PropertyCache propertyCacheCopy)
        {
            _logger.LogInformation($"Simulation {simulationConfiguration} ({simulationConfiguration.SimulationTicks.Count()} ticks)");
            IModel model = null;
            if (!(fmuDict.TryGetValue(fmuFilePath, out model))) {
               _logger.LogInformation("Load Model");
               model = Model.Load(fmuFilePath);
               fmuDict.Add(fmuFilePath, model);
            }

            // This instantiation fails frequently due to a "protected memory" exception(even when no other simulations have been run beforehand). Because it's thrown from
            // external code, the exception can't be caught for retries. This only works consistently with the Modelica reference FMUs.
            IInstance fmuInstance = null;
            if (!(iDict.TryGetValue("demo"+iCount, out fmuInstance))) {
               _logger.LogInformation($"Create instance {iCount}.");
               fmuInstance = model.CreateCoSimulationInstance("demo"+iCount);
               iDict.Add("demo"+iCount, fmuInstance);

               _logger.LogInformation("Setting time");
               fmuInstance.StartTime(0);

            }
            // FIXME: we're currently using the cached instance without resetting time. We can't reliably get fresh instances.
            // iCount++;

            // Run the simulation by executing ActuationActions in their respective simulation intervals.
            foreach (var simulationTick in simulationConfiguration.SimulationTicks)
            {
                var fmuActuationInputs = new List<(string, string, object)>();

                // Get all ObservableProperties and add them to the inputs for the FMU.
                var observableProperties = GetObservablePropertiesFromPropertyCache(instanceModel, propertyCacheCopy);

                foreach (var observableProperty in observableProperties)
                {
                    // Shave off the long name URIs from the instance model.
                    var simpleObservablePropertyName = MapekUtilities.GetSimpleName(observableProperty.Name);
                    fmuActuationInputs.Add((simpleObservablePropertyName, observableProperty.OwlType, observableProperty.Value));
                }

                // Add all ActuatorStates to the inputs for the FMU.
                foreach (var actuationAction in simulationTick.ActionsToExecute)
                {
                    // Shave off the long name URIs from the instance model.
                    var simpleActuatorName = MapekUtilities.GetSimpleName(actuationAction.Actuator.Name);
                    fmuActuationInputs.Add((simpleActuatorName + "State", "int", actuationAction.NewStateValue));
                }

                _logger.LogInformation($"Parameters: {string.Join(", ",fmuActuationInputs.Select(i => i.ToString()))}");
                AssignSimulationInputsToParameters(model, fmuInstance, fmuActuationInputs);

                // The time in seconds isn't translated properly which means that the results come out differently from the FMUs.
                _logger.LogInformation("Tick");
                fmuInstance.AdvanceTime(simulationTick.TickDurationSeconds);

                AssignPropertyCacheCopyValues(fmuInstance, propertyCacheCopy, model.Variables);
            }

            // Calling Dispose() on the instance creates a problem in the underlying external code which crashes the application approximately 95% of the time.
            // This could be due to improper implementations or handling of resources in the Femyou (.NET) library used to read from and write to FMUs. Note that
            // calling Dispose() while running a Modelica reference FMU (against which the Femyou library was checked), this issue doesn't occur. Our FMUs are
            // generated as standard FMUs by OpenModelica.
            // _logger.LogInformation("Dispose...");
            // fmuInstance.Dispose();
            // _logger.LogInformation("...done");
            // model.Dispose();
        }

        private void AssignSimulationInputsToParameters(IModel model, IInstance fmuInstance, IEnumerable<(string, string, object)> fmuInputs)
        {
            foreach (var input in fmuInputs)
            {
                var valueHandler = _factory.GetValueHandlerImplementation(input.Item2);
                var fmuVariable = model.Variables[input.Item1];

                valueHandler.WriteValueToSimulationParameter(fmuInstance, fmuVariable, input.Item3);
            }
        }

        private void AssignPropertyCacheCopyValues(IInstance fmuInstance, PropertyCache propertyCacheCopy, IReadOnlyDictionary<string, IVariable> fmuOutputs)
        {
            // Find the correct Property from the simpler output variable name and assign its value.
            foreach (var fmuOutput in fmuOutputs)
            {
                foreach (var propertyName in propertyCacheCopy.Properties.Keys)
                {
                    if (propertyName.EndsWith($"#{fmuOutput.Key}"))
                    {
                        var valueHandler = _factory.GetValueHandlerImplementation(propertyCacheCopy.Properties[propertyName].OwlType);
                        var value = valueHandler.GetValueFromSimulationParameter(fmuInstance, fmuOutput.Value);

                        _logger.LogInformation($"New value for {propertyName}: {value}");
                        propertyCacheCopy.Properties[propertyName].Value = value;
                    }
                }
            }
        }

        private int GetNumberOfSatisfiedOptimalConditions(IEnumerable<OptimalCondition> optimalConditions, PropertyCache propertyCache)
        {
            var numberOfSatisfiedOptimalConditions = 0;

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
                        numberOfSatisfiedOptimalConditions++;

                        break;
                    }
                }
            }

            return numberOfSatisfiedOptimalConditions;
        }

        private SimulationConfiguration GetOptimalConfiguration(IGraph instanceModel,
            PropertyCache propertyCache,
            IEnumerable<OptimalCondition> optimalConditions,
            IEnumerable<SimulationConfiguration> simulationConfigurations)
        {
            // This method is a filter for finding the optimal simulation configuration. It works in a few steps of descending precedance, each of which further reduces the set of
            // simulation configurations:
            // 1. Filter for simulation configurations that satisfy the most OptimalConditions.
            // 2. Filter for simulation configurations that have the highest number of the most optimized Properties.
            // 3. Pick the first one.

            // Filter for simulation configurations that satisfy the most OptimalConditions.
            var simulationConfigurationsWithMostOptimalConditionsSatisfied = GetSimulationConfigurationsWithMostOptimalConditionsSatisfied(simulationConfigurations, optimalConditions);

            if (simulationConfigurationsWithMostOptimalConditionsSatisfied.Count() == 1)
            {
                return simulationConfigurationsWithMostOptimalConditionsSatisfied.First();
            }

            _logger.LogInformation("{count} simulation configurations remaining after the first filter.", simulationConfigurationsWithMostOptimalConditionsSatisfied.Count());

            // Filter for simulation configurations that optimize the most targeted Properties.
            var simulationConfigurationsWithMostOptimizedProperties = GetSimulationConfigurationsWithMostOptimizedProperties(simulationConfigurationsWithMostOptimalConditionsSatisfied, instanceModel, propertyCache);

            if (simulationConfigurationsWithMostOptimizedProperties.Count() == 1)
            {
                return simulationConfigurationsWithMostOptimizedProperties.First();
            }

            _logger.LogInformation("{count} simulation configurations remaining after the second filter.", simulationConfigurationsWithMostOptimizedProperties.Count());

            // At this point, arbitrarily return the first one regardless of the number of simulation configurations remaining.
            return simulationConfigurationsWithMostOptimizedProperties.First();
        }

        private IEnumerable<SimulationConfiguration> GetSimulationConfigurationsWithMostOptimalConditionsSatisfied(IEnumerable<SimulationConfiguration> simulationConfigurations,
            IEnumerable<OptimalCondition> optimalConditions)
        {
            var passingSimulationConfigurations = new List<SimulationConfiguration>();
            var highestNumberOfSatisfiedOptimalConditions = 0;

            foreach (var simulationConfiguration in simulationConfigurations)
            {
                var numberOfSatisfiedOptimalConditions = GetNumberOfSatisfiedOptimalConditions(optimalConditions, simulationConfiguration.ResultingPropertyCache);

                if (numberOfSatisfiedOptimalConditions > highestNumberOfSatisfiedOptimalConditions)
                {
                    highestNumberOfSatisfiedOptimalConditions = numberOfSatisfiedOptimalConditions;
                    passingSimulationConfigurations = new List<SimulationConfiguration>
                    {
                        simulationConfiguration
                    };
                }
                else if (numberOfSatisfiedOptimalConditions == highestNumberOfSatisfiedOptimalConditions)
                {
                    passingSimulationConfigurations.Add(simulationConfiguration);
                }
            }

            return passingSimulationConfigurations;
        }

        private IEnumerable<SimulationConfiguration> GetSimulationConfigurationsWithMostOptimizedProperties(IEnumerable<SimulationConfiguration> simulationConfigurations,
            IGraph instanceModel,
            PropertyCache propertyCache)
        {
            var propertyChangesToOptimizeFor = GetPropertyChangesToOptimizeFor(instanceModel, propertyCache);
            var valueHandlers = propertyChangesToOptimizeFor.Select(p => _factory.GetValueHandlerImplementation(p.Property.OwlType));

            _logger.LogInformation("Ordering and filtering simulation results...");
            
            var simulationConfigurationComparer = new SimulationConfigurationComparer(propertyChangesToOptimizeFor.Zip(valueHandlers));

            // Return the simulation configurations with the maximum score.
            return simulationConfigurations.OrderByDescending(s => s, simulationConfigurationComparer)
                .Where(s => simulationConfigurationComparer.Compare(s, simulationConfigurations.First()) > -1);
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

            var queryResult = instanceModel.ExecuteQuery(query, _logger);

            foreach (var result in queryResult.Results)
            {
                var propertyChangeName = result["propertyChange"].ToString();
                var propertyName = result["property"].ToString();
                var effectName = result["effect"].ToString().Split("/")[^1];

                Property property = null!;

                var propertyFound = propertyCache.Properties.TryGetValue(propertyName, out property!);

                // Check where in the property cache the Property is. Shouldn't really fail.
                if (!propertyFound)
                {
                    property = propertyCache.ConfigurableParameters[propertyName];
                }

                if (!Enum.TryParse(effectName, out Effect effect))
                {
                    throw new Exception($"Enum value {effectName} is not supported.");
                }

                // TODO: Review, fishy constructing PropertyChange with `null`, should probably be non-nullable?
                Debug.Assert(property != null, $"Didn't find {propertyName}.");
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

        private void LogOptimalSimulationConfiguration(SimulationConfiguration optimalSimulationConfiguration)
        {
            var logMsg = "Chosen optimal configuration, Actuation actions:\n";

            // Convert to a list to use indexing.
            var simulationTickList = optimalSimulationConfiguration.SimulationTicks.ToList();

            for (var i = 0; i < simulationTickList.Count; i++)
            {
                logMsg += $"Interval {i + 1}:\n";

                foreach (var action in simulationTickList[i].ActionsToExecute)
                {
                    logMsg += $"Actuator: {action.Actuator.Name}, Actuator state: {action.NewStateValue.ToString()}\n";
                }
            }

            logMsg += "Post-tick actions:\n";

            foreach (var postTickAction in optimalSimulationConfiguration.PostTickActions)
            {
                logMsg += $"Configurable parameter: {postTickAction.ConfigurableParameter.Name}; ";
                logMsg += $"New Value: {postTickAction.NewParameterValue.ToString()}\n";
            }
            _logger.LogInformation(logMsg);
        }
    }
}
