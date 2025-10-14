using Femyou;
using Logic.FactoryInterface;
using Logic.Mapek.Comparers;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using System.Text;
using VDS.RDF;

namespace Logic.Mapek
{
    internal class MapekPlan : IMapekPlan
    {
        // Required as fields to preserve caching throughout multiple MAPE-K look cycles.
	    public static Dictionary<string, IModel> _fmuDict = new Dictionary<string, IModel>();
	    public static Dictionary<string, IInstance> _iDict = new Dictionary<string, IInstance>();

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
            return GetNaryCartesianProducts(actuatorActuationsByActuator);
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
            return GetNaryCartesianProducts(reconfigurationActionsByConfigurableParameter);
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
            // Retrieve the host platform FMU and its simulation fidelity for ActuationAction simulations.
            var fmuModel = GetHostPlatformFmuModel(instanceModel, simulationConfigurations.First(), fmuDirectory);

            int i = 0;
            // TODO: Parallelize simulations (#13).
            foreach (var simulationConfiguration in simulationConfigurations)
            {
                _logger.LogInformation("Running simulation #{run}", i++);

                // Make a deep copy of the property cache for the current simulation configuration.
                var propertyCacheCopy = GetPropertyCacheCopy(propertyCache);

                if (simulationConfiguration.SimulationTicks.Any())
                {
                    ExecuteActuationActionFmu(fmuModel.FilePath, simulationConfiguration, instanceModel, propertyCacheCopy, fmuModel.SimulationFidelitySeconds);
                }

                if (simulationConfiguration.PostTickActions.Any())
                {
                    // Executing/simulating soft sensors during the Plan phase is not yet supported.
                }

                // Assign the final Property values to the results of the simulation configuration.
                simulationConfiguration.ResultingPropertyCache = propertyCacheCopy;
            }
        }

        private FmuModel GetHostPlatformFmuModel(IGraph instanceModel, SimulationConfiguration simulationConfiguration, string fmuDirectory)
        {
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

            query.CommandText = @"SELECT ?fmuModel ?fmuFilePath ?simulationFidelitySeconds WHERE {
                ?platform rdf:type sosa:Platform . " +
                clause +
                @"?platform meta:hasSimulationModel ?fmuModel .
                ?fmuModel rdf:type meta:FmuModel .
                ?fmuModel meta:hasURI ?fmuFilePath .
                ?fmuModel meta:hasSimulationFidelitySeconds ?simulationFidelitySeconds . }";

            var queryResult = instanceModel.ExecuteQuery(query, _logger);

            // There can theoretically be multiple Platforms hosting the same Actuator, but we limit ourselves to expect a single Platform
            // per instance model. There should therefore be only one result.
            var fmuModel = queryResult.Results[0];

            return new FmuModel
            {
                Name = fmuModel["fmuModel"].ToString(),
                FilePath = Path.Combine(fmuDirectory, fmuModel["fmuFilePath"].ToString().Split('^')[0]),
                SimulationFidelitySeconds = int.Parse(fmuModel["simulationFidelitySeconds"].ToString().Split('^')[0])
            };
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

        private void ExecuteActuationActionFmu(string fmuFilePath,
            SimulationConfiguration simulationConfiguration,
            IGraph instanceModel,
            PropertyCache propertyCacheCopy,
            int simulationFidelitySeconds)
        {
            _logger.LogInformation($"Simulation {simulationConfiguration} ({simulationConfiguration.SimulationTicks.Count()} ticks)");
            if (!_fmuDict.TryGetValue(fmuFilePath, out IModel? model))
            {
                _logger.LogDebug("Loading Model {filePath}", fmuFilePath);
                model = Model.Load(fmuFilePath);
                _fmuDict.Add(fmuFilePath, model);
            }
            Debug.Assert(model != null, "Model is null after loading.");
            // We're only using one instance per FMU, so we can just use the path as name.
            var instanceName = fmuFilePath;
            if (!_iDict.TryGetValue(instanceName, out IInstance? fmuInstance)) {
                _logger.LogDebug("Creating instance.");
                fmuInstance = model.CreateCoSimulationInstance(instanceName);
                _iDict.Add(instanceName, fmuInstance);

                _logger.LogDebug("Setting time");
                fmuInstance.StartTime(0);
            } else {
                _logger.LogDebug("Resetting.");
                fmuInstance.Reset();
                fmuInstance.StartTime(0);
            }
            Debug.Assert(fmuInstance != null, "Instance is null after creation.");

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

                _logger.LogInformation($"Parameters: {string.Join(", ", fmuActuationInputs.Select(i => i.ToString()))}");
                AssignSimulationInputsToParameters(model, fmuInstance, fmuActuationInputs);

                _logger.LogInformation("Tick");
                // Keep simulation fidelity while advancing an appropriate amount of time.
                var maximumSteps = simulationTick.TickDurationSeconds / simulationFidelitySeconds;
                var maximumStepsRoundedDown = (int)Math.Floor(maximumSteps);
                var difference = maximumSteps - maximumStepsRoundedDown;

                for (var i = 0; i < maximumStepsRoundedDown; i++)
                {
                    fmuInstance.AdvanceTime(simulationFidelitySeconds);
                }

                // Advance the remainder of time to stay true to the simulation interval duration.
                fmuInstance.AdvanceTime(difference);

                AssignPropertyCacheCopyValues(fmuInstance, propertyCacheCopy, model.Variables);
            }
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

                        _logger.LogInformation("New value for {propertyName}: {value}", propertyName, value);
                        propertyCacheCopy.Properties[propertyName].Value = value;
                    }
                }
            }
        }

        private int GetNumberOfSatisfiedOptimalConditions(IEnumerable<OptimalCondition> optimalConditions, PropertyCache propertyCache)
        {
            var numberOfSatisfiedOptimalConditions = optimalConditions.Count();

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
                        numberOfSatisfiedOptimalConditions--;

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
