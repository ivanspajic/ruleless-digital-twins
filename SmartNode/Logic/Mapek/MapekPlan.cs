using Femyou;
using Logic.FactoryInterface;
using Logic.Mapek.Comparers;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics;
using VDS.RDF;
using static Femyou.IModel;

namespace Logic.Mapek
{
    public class MapekPlan : IMapekPlan
    {
        private const int MaximumSimulationTimeSeconds = 900; // XXX parametrize

        // Required as fields to preserve caching throughout multiple MAPE-K loop cycles.
        private readonly Dictionary<string, IModel> _fmuDict = [];
	    private readonly Dictionary<string, IInstance> _iDict = [];
        // A setting that determines whether the DT operates in the 'reactive' (true) or 'proactive' (false) mode. The reactive mode generates only those Actions that will
        // act as mitigations against violated OptimalConditions. The proactive mode generates all possible Actions from all Actuators and/or ConfigurableParameters in the
        // instance model.
        private readonly bool _restrictToReactiveActionsOnly;
        // Used for performance enhancements.
        private bool _restrictToReactiveActionsOnlyOld = true;

        private readonly ILogger<IMapekPlan> _logger;
        private readonly IFactory _factory;
        private readonly IMapekKnowledge _mapekKnowledge;
        private readonly FilepathArguments _filepathArguments;

        public MapekPlan(IServiceProvider serviceProvider, bool restrictToReactiveActionsOnly = true)
        {
            _restrictToReactiveActionsOnly = restrictToReactiveActionsOnly;
            _restrictToReactiveActionsOnlyOld = _restrictToReactiveActionsOnly;
            _logger = serviceProvider.GetRequiredService<ILogger<IMapekPlan>>();
            _factory = serviceProvider.GetRequiredService<IFactory>();
            _mapekKnowledge = serviceProvider.GetRequiredService<IMapekKnowledge>();
            _filepathArguments = serviceProvider.GetRequiredService<FilepathArguments>();
        }

        public SimulationPath Plan(Cache cache, int lookAheadCycles) {
            _logger.LogInformation("Starting the Plan phase.");

            _logger.LogInformation("Generating simulations.");

            // TODO: This is now in Plan(), but we may need it here as well later?
            // This is necessary for the fitness function. This might change as we reevaluate how the fitness function should work
            // and how it should be specified.
            // var optimalConditions = GetAllOptimalConditions(propertyCache);

            // Get all combinations of possible simulation configurations for the given number of cycles.
            var simulationTree = new SimulationTreeNode {
                NodeItem = new Simulation(cache.PropertyCache),
                Children = []
            };
            var simulations = GetSimulationsAndGenerateSimulationTree(lookAheadCycles, 0, simulationTree, false, true, new List<List<ActuationAction>>(), cache.PropertyCache);

            // Execute the simulations and obtain their results.
            Simulate(simulations, cache.SoftSensorTreeNodes);

            _logger.LogInformation("Generated a total of {total} simulation paths.", simulationTree.SimulationPaths.Count());
            
            // This is necessary for the fitness function. This might change as we reevaluate how the fitness function should work
            // and how it should be specified.
            var optimalConditions = GetAllOptimalConditions(cache.PropertyCache);

            // Find the optimal simulation path.
            var optimalSimulationPath = GetOptimalSimulationPath(cache.PropertyCache, optimalConditions, simulationTree.SimulationPaths);

            LogOptimalSimulationPath(optimalSimulationPath);

            return optimalSimulationPath!;
        }

        // TODO: consider making this async in the future.
        // The boolean flags are used for performance improvements.
        internal IEnumerable<Simulation> GetSimulationsAndGenerateSimulationTree(int lookAheadCycles,
            int currentCycle,
            SimulationTreeNode simulationTreeNode,
            bool unrestrictedInferenceExecuted,
            bool reloadInferredModel,
            IEnumerable<IEnumerable<Models.OntologicalModels.Action>> actionCombinations,
            PropertyCache propertyCache) {
            // Update the restriction setting in the instance model to run the inference rules correctly.
            EnsureUpdatedRestrictionSetting();

            if (_restrictToReactiveActionsOnly) {
                // If the RDT runs in reactive mode, then write the current simulation's values into the instance model for
                // dynamic OptimalCondition evaluation and ActionCombination generation.
                if (simulationTreeNode.NodeItem.Index != -1) {
                    UpdateInstanceModelWithSimulationValues(simulationTreeNode.NodeItem.PropertyCache!);
                }
                InferActionCombinations();

                // Check the performance flags for rerunning the inference engine and reloading the instance model.
                unrestrictedInferenceExecuted = false;
                reloadInferredModel = true;
            } else if (!_restrictToReactiveActionsOnly && !unrestrictedInferenceExecuted) {
                InferActionCombinations();
                // If the RDT runs in proactive mode, then we don't have to rerun the inference until the setting is changed.
                unrestrictedInferenceExecuted = true;
            }

            // Only reload the instance model if a new set of ActionCombinations has been inferred.
            if (reloadInferredModel) {
                actionCombinations = GetActionCombinations(propertyCache);

                reloadInferredModel = false;
            }

            // For every ActionCombination, create a new Simulation, yield return it, and continue the process recursively for all children
            // as long as there are additional MAPE-K cycles to simulate for.
            var simulationTreeNodeChildren = new List<SimulationTreeNode>();

            foreach (var actionCombination in actionCombinations) {
                // TODO: partition more efficiently:
                // var actionPartition = actionCombination.ToLookup(action => action is FMUParameterAction);
                var simulation = new Simulation(GetPropertyCacheCopy(simulationTreeNode.NodeItem.PropertyCache!)) {
                    Actions = actionCombination.Where(action => action is not FMUParameterAction).ToList(),
                    InitializationActions = actionCombination.Where(action => action is FMUParameterAction).Select(a => (FMUParameterAction)a).ToList(),
                    Index = currentCycle
                };

                // Already stream back the newly-created simulation.
                yield return simulation;

                // Choose whether to keep the current simulation after its property cache values are populated. This allows for dynamic tree
                // pruning as the tree is being constructed for better performance.
                var keepSimulation = GetKeepSimulation(simulation);

                var currentSimulationTreeNode = new SimulationTreeNode {
                    NodeItem = simulation,
                    Children = []
                };

                // If there are more cycles to simulate for, and if we're keeping the current simulation, then keep expanding branches on the tree.
                if (currentCycle < lookAheadCycles - 1 && keepSimulation) {
                    var childSimulations = GetSimulationsAndGenerateSimulationTree(lookAheadCycles,
                        currentCycle + 1,
                        currentSimulationTreeNode,
                        unrestrictedInferenceExecuted,
                        reloadInferredModel,
                        actionCombinations,
                        propertyCache);

                    foreach (var childSimulation in childSimulations) {
                        yield return childSimulation;
                    }
                }

                simulationTreeNodeChildren.Add(currentSimulationTreeNode);
            }

            simulationTreeNode.Children = simulationTreeNodeChildren;
        }

        // Updates the setting for restricting Actions and thus ActionCombinations only to those mitigating OptimalConditions.
        private void EnsureUpdatedRestrictionSetting() {
            // Improves performance by not unnecessarily writing to disk.
            if (_restrictToReactiveActionsOnly == _restrictToReactiveActionsOnlyOld) {
                return;
            } else {
                _restrictToReactiveActionsOnlyOld = _restrictToReactiveActionsOnly;
            }

            var query = _mapekKnowledge.GetParameterizedStringQuery(@"DELETE {
                ?platform meta:generateCombinationsOnlyFromOptimalConditions ?oldValue .
            }
            INSERT {
                ?platform meta:generateCombinationsOnlyFromOptimalConditions @newValue .
            }
            WHERE {
                ?platform rdf:type sosa:Platform .
                ?platform meta:generateCombinationsOnlyFromOptimalConditions ?oldValue .
            }");

            query.SetLiteral("newValue", _restrictToReactiveActionsOnly);

            // Update the instance model and commit its contents to the disk.
            _mapekKnowledge.UpdateModel(query);
            _mapekKnowledge.CommitInMemoryInstanceModelToKnowledgeBase();
        }

        private void UpdateInstanceModelWithSimulationValues(PropertyCache simulationPropertyCache) {
            foreach (var configurableParameterKeyValue in simulationPropertyCache.ConfigurableParameters) {
                _mapekKnowledge.UpdateConfigurableParameterValue(configurableParameterKeyValue.Value);
            }

            foreach (var propertyKeyValue in simulationPropertyCache.Properties) {
                _mapekKnowledge.UpdatePropertyValue(propertyKeyValue.Value);
            }

            _mapekKnowledge.CommitInMemoryInstanceModelToKnowledgeBase();
        }

        protected virtual void InferActionCombinations() {
            // Execute the inference engine as an external process.
            var processInfo = new ProcessStartInfo {
                FileName = "java", // Assumes JAVA is registered in the PATH environment variable (or equivalent).
                Arguments = $"-jar \"{_filepathArguments.InferenceEngineFilepath}\" " +
                    $"\"{_filepathArguments.OntologyFilepath}\" " +
                    $"\"{_filepathArguments.InstanceModelFilepath}\" " +
                    $"\"{_filepathArguments.InferenceRulesFilepath}\" " +
                    $"\"{_filepathArguments.InferredModelFilepath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = Directory.GetParent(_filepathArguments.InstanceModelFilepath)!.FullName
            };

            _logger.LogInformation("Inferring action combinations.");
            using var process = Process.Start(processInfo);
            Debug.Assert(process != null, "Process failed to start.");

            process!.OutputDataReceived += (sender, e) => {
                _logger.LogInformation(e.Data);
            };

            process.ErrorDataReceived += (sender, e) => {
                _logger.LogInformation(e.Data);
            };

            _logger.LogInformation("Process started with ID {processId}.", process.Id);
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            if (process.ExitCode != 0) {
                throw new Exception($"The inference engine encountered an error. Process {process.Id} exited with code {process.ExitCode}.");
            }

            _logger.LogInformation("Process {processId} exited with code {processExitCode}.", process.Id, process.ExitCode);
        }

        // This method currently only supports ActuationActions.
        private List<List<Models.OntologicalModels.Action>> GetActionCombinations(PropertyCache propertyCache) {
            var actionCombinations = new List<List<Models.OntologicalModels.Action>>();

            var actionCombinationQuery = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT ?actionCombination (GROUP_CONCAT(?action; SEPARATOR="" "") AS ?actions) WHERE {
	                ?actionCombination rdf:type meta:ActionCombination .
	                FILTER NOT EXISTS {
		                {
			                ?actionCombination rdf:comment ""duplicate""^^<http://www.w3.org/2001/XMLSchema#string> .
		                }
		                UNION
		                {
			                ?actionCombination rdf:comment ""not final""^^<http://www.w3.org/2001/XMLSchema#string> .
		                }
	                }
	                ?actionCombination meta:hasActions ?actionList .
	                ?actionList rdf:rest*/rdf:first ?action . }
                GROUP BY ?actionCombination");

            // Make sure the updated inferred model is reloaded before querying for ActionCombinations.
            _mapekKnowledge.LoadModelsFromKnowledgeBase();
            
            var actionCombinationQueryResult = _mapekKnowledge.ExecuteQuery(actionCombinationQuery, true);

            actionCombinationQueryResult.Results.ForEach(combinationResult => {
                // ActionCombinations can only be queried through concatenation in the query above, so they must be split.
                var actions = combinationResult["actions"].ToString().Split('^')[0].Split(' ').ToList();

                var actionCombination = new List<Models.OntologicalModels.Action>();
                var fmuInitActions = new List<FMUParameterAction>();

                var actuationActionQuery = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT ?actuator ?actuatorState ?actuatorName ?isParameter WHERE {
                        @action rdf:type meta:ActuationAction .
                        @action meta:hasActuator ?actuator .
                        OPTIONAL { ?actuator meta:hasActuatorName ?actuatorName } .
                        OPTIONAL { ?actuator meta:isParameter ?isParameter } .
                        @action meta:hasActuatorState ?actuatorState . }");
                actions.ForEach(action => {
                    // For each Action, find the appropriate Actuator and its state.
                    // Ideally magic strings here should really be linked to/into the query-string.
                    actuationActionQuery.SetUri("action", new Uri(action));
                    var actuationActionQueryResult = _mapekKnowledge.ExecuteQuery(actuationActionQuery, true);

                    actuationActionQueryResult.Results.ForEach(actionResult => {
                        var actuatorName = actionResult["actuator"].ToString();

                        var split = actionResult["actuatorState"].ToString().Split("^^");
                        var actuatorState = split[0];
                        var actuatorType = split[1];
                        string? paramName = null; // Apologies for the confusing attribute name (see `actuatorName` above).
                        if (actionResult.TryGetValue("actuatorName", out var paramNameNode)) {
                            paramName = paramNameNode.ToString().Split('^')[0];
                        }
                        // Check if we're dealing with initialization of the FMU:
                        bool isParameter = false;
                        if (actionResult.TryGetValue("isParameter", out var isParameterNode)) {
                            string isParamStr = isParameterNode.ToString().Split('^')[0];
                            isParameter = isParamStr == "true";
                        }

                        // We'll filter those later:
                        if (isParameter) {
                            actionCombination.Add(new FMUParameterAction {
                            Name = action,
                            Actuator = new Actuator {
                                Name = actuatorName,
                                ParameterName = paramName,
                                Type = actuatorType
                            },
                            NewStateValue = actuatorState
                            });
                        } else {
                            actionCombination.Add(new ActuationAction {
                            Name = action,
                            Actuator = new Actuator {
                                Name = actuatorName,
                                ParameterName = paramName,
                                Type = actuatorType
                            },
                            NewStateValue = actuatorState
                            });
                        }
                    });
                });

                // Query for ReconfigurationAction contents.
                var reconfigurationActionQuery = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT ?configurableParameter ?newValue WHERE {
                        @action rdf:type meta:ReconfigurationAction .
                        @action ssn:forProperty ?configurableParameter .
                        @action meta:hasValue ?newValue . }");
                actions.ForEach(action => {
                    // For each Action, find the appropriate ConfigurableParameter and its value.
                    reconfigurationActionQuery.SetUri("action", new Uri(action));
                    var reconfigurationActionQueryResult = _mapekKnowledge.ExecuteQuery(reconfigurationActionQuery, true);

                    reconfigurationActionQueryResult.Results.ForEach(actionResult => {
                        var configurableParameterName = actionResult["configurableParameter"].ToString();
                        var newValue = actionResult["newValue"].ToString().Split('^')[0];

                        actionCombination.Add(new ReconfigurationAction {
                            ConfigurableParameter = propertyCache.ConfigurableParameters[configurableParameterName],
                            Name = action,
                            NewParameterValue = newValue
                        });
                    });
                });

                actionCombinations.Add(actionCombination);
            });

            return actionCombinations;
        }

        private bool GetKeepSimulation(Simulation simulation) {
            // Here we can implement dynamic pruning logic based on the values of the simulation's property cache.
            return true;
        }

        public static IEnumerable<IEnumerable<T>> GetNaryCartesianProducts<T> (IEnumerable<IEnumerable<T>> sequences)
        {
            IEnumerable<IEnumerable<T>> emptyProduct = new[] { Enumerable.Empty<T>() };
            return sequences.Aggregate(
              emptyProduct,
              (accumulator, sequence) =>
                from accseq in accumulator
                from item in sequence
                select accseq.Concat(new[] { item }));
        }

        internal void Simulate(IEnumerable<Simulation> simulations, IEnumerable<SoftSensorTreeNode> softSensorTreeNodes)
        {
            // Measure simulation time.
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            int i = 0;
            // TODO: Parallelize simulations (#13).
            foreach (var simulation in simulations)
            {
                // Retrieve the host platform FMU and its simulation fidelity for ActuationAction simulations.
                var fmuModel = GetHostPlatformFmuModel(simulation, _filepathArguments.FmuDirectory);

                _logger.LogInformation("Running simulation #{run}", i++);

                // Perform the simulation via FMU execution and ensure all the Properties in the simulation's property cache are updated by running all soft sensors
                // in the correct order.
                ExecuteActuationActionFmu(fmuModel, simulation);
                ExecuteSoftSensorsAndUpdateSimulationCache(simulation, softSensorTreeNodes);
            }

            stopwatch.Stop();
            _logger.LogInformation("Total simulation time (seconds): {elapsedTime}", (double)stopwatch.ElapsedMilliseconds / 1000);
        }
        
        private static void ExecuteSoftSensorsAndUpdateSimulationCache(Simulation simulation, IEnumerable<SoftSensorTreeNode> softSensorTreeNodes) {
            // Execute the tree of soft sensors in the correct order to ensure all Properties in the simulation's property cache are updated.
            foreach (var softSensorTreeNode in softSensorTreeNodes) {
                if (softSensorTreeNode.Children.Any()) {
                    ExecuteSoftSensorsAndUpdateSimulationCache(simulation, softSensorTreeNode.Children);

                    var inputs = new List<object>();
                    foreach (var softSensorTreeNodeChild in softSensorTreeNode.Children) {
                        var inputProperty = simulation.PropertyCache!.Properties[softSensorTreeNodeChild.OutputProperty];
                        inputs.Add(inputProperty.Value);
                    }

                    var propertyValue = softSensorTreeNode.NodeItem.ObservePropertyValue(inputs.ToArray());
                    var property = simulation.PropertyCache!.Properties[softSensorTreeNode.OutputProperty];
                    property.Value = propertyValue;
                }
            }
        }

        private static PropertyCache GetPropertyCacheCopy(PropertyCache originalPropertyCache)
        {
            var propertyCacheCopy = new PropertyCache
            {
                Properties = new Dictionary<string, Property>(),
                ConfigurableParameters = new Dictionary<string, ConfigurableParameter>(),
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

        private List<Property> GetObservablePropertiesFromPropertyCache(PropertyCache propertyCache)
        {
            var observableProperties = new List<Property>();

            var query = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT DISTINCT ?observableProperty WHERE {
                ?sensor rdf:type sosa:Sensor .
                ?sensor sosa:observes ?observableProperty . }");

            var queryResult = _mapekKnowledge.ExecuteQuery(query);

            foreach (var result in queryResult.Results)
            {
                var propertyName = result["observableProperty"].ToString();

                if (propertyCache.Properties.TryGetValue(propertyName, out Property? property))
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

        public FmuModel GetHostPlatformFmuModel(Simulation simulation, string fmuDirectory) {
            // Retrieve the Platform (TT) FMU to be used for Actuators and/or ConfigurableParameters.

            var query = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT ?fmuModel ?fmuFilePath ?simulationFidelitySeconds WHERE {
                ?platform rdf:type sosa:Platform .
                ?platform meta:hasSimulationModel ?fmuModel .
                ?fmuModel rdf:type meta:FmuModel .
                ?fmuModel meta:hasURI ?fmuFilePath .
                ?fmuModel meta:hasSimulationFidelitySeconds ?simulationFidelitySeconds . }");

            var queryResult = _mapekKnowledge.ExecuteQuery(query);

            // There can theoretically be multiple Platforms hosting the same Actuator, but we limit ourselves to expect a single Platform
            // per instance model. There should therefore be only one result.
            var fmuModel = queryResult.Results[0];

            return new FmuModel {
                Name = fmuModel["fmuModel"].ToString(),
                Filepath = Path.Combine(fmuDirectory, fmuModel["fmuFilePath"].ToString().Split('^')[0]),
                SimulationFidelitySeconds = int.Parse(fmuModel["simulationFidelitySeconds"].ToString().Split('^')[0])
            };
        }

        // Initialize the FMU between enter/exitInitialization (#42).
        // TODO: No one is generating those yet ;-)
        protected virtual bool Initialization(Simulation simulation, IModel model, IInstance fmuInstance) {
            var actions = simulation.InitializationActions.Select(action => (action.Actuator.ParameterName ?? MapekUtilities.GetSimpleName(action.Name), action.Actuator.Type!, action.NewStateValue)).ToList();
            AssignSimulationInputsToParameters(model, fmuInstance, actions);
            return true;
        }
        private void ExecuteActuationActionFmu(FmuModel fmuModel, Simulation simulation) {
            // The LogDebug calls here are primarily to keep an eye on crashes in the FMU which are otherwise a tad harder to track down.
            _logger.LogInformation("Simulation {simulation}", simulation); // XXX Arg useless.
            if (!_fmuDict.TryGetValue(fmuModel.Filepath, out IModel? model)) {
                _logger.LogDebug("Loading Model {filePath}", fmuModel.Filepath);
                model = Model.Load(fmuModel.Filepath, new Collection<UnsupportedFunctions>([UnsupportedFunctions.SetTime2]));
                _fmuDict.Add(fmuModel.Filepath, model);
            }
            Debug.Assert(model != null, "Model is null after loading.");
            // We're only using one instance per FMU, so we can just use the path as name.
            var instanceName = fmuModel.Filepath;
            if (!_iDict.TryGetValue(instanceName, out IInstance? fmuInstance)) {
                _logger.LogDebug("Creating instance.");
                fmuInstance = model.CreateCoSimulationInstance(instanceName);
                _iDict.Add(instanceName, fmuInstance);
            } else {
                _logger.LogDebug("Resetting.");
                fmuInstance.Reset();
            }
            Debug.Assert(fmuInstance != null, "Instance is null after creation.");
            _logger.LogDebug("Setting time {t}", simulation.Index * MaximumSimulationTimeSeconds);
            fmuInstance.StartTime(simulation.Index * MaximumSimulationTimeSeconds, (i) => Initialization(simulation, model, i));

            // Run the simulation by executing ActuationActions.
            var fmuActuationInputs = new List<(string, string, object)>();

            // Get all ObservableProperties and add them to the inputs for the FMU.
            var observableProperties = GetObservablePropertiesFromPropertyCache(simulation.PropertyCache!);

            foreach (var observableProperty in observableProperties) {
                // Shave off the long name URIs from the instance model.
                var simpleObservablePropertyName = MapekUtilities.GetSimpleName(observableProperty.Name);
                fmuActuationInputs.Add((simpleObservablePropertyName, observableProperty.OwlType, observableProperty.Value));
            }

            // Add all ActuatorStates to the inputs for the FMU.
            foreach (var action in simulation.Actions)
            {
                string name;
                string type;
                object value;
                if (action is ActuationAction actuationAction) {
                    name = actuationAction.Actuator.Name;
                    type = actuationAction.Actuator.Type!;
                    value = actuationAction.NewStateValue;
                } else {
                    var reconfigurationAction = (ReconfigurationAction)action;
                    name = reconfigurationAction.ConfigurableParameter.Name;
                    type = reconfigurationAction.ConfigurableParameter.OwlType;
                    value = reconfigurationAction.NewParameterValue;
                }

                // Shave off the long name URIs from the instance model.
                var simpleName = MapekUtilities.GetSimpleName(name);
                fmuActuationInputs.Add((simpleName, type, value));
            }

            _logger.LogInformation("Parameters: {p}", string.Join(", ", fmuActuationInputs.Select(i => i.ToString())));
            AssignSimulationInputsToParameters(model, fmuInstance, fmuActuationInputs);

            _logger.LogDebug("Tick");
            // Advance the FMU time for the duration of the simulation tick in steps of simulation fidelity.
            var maximumSteps = (double)MaximumSimulationTimeSeconds / fmuModel.SimulationFidelitySeconds;
            var maximumStepsRoundedDown = (int)Math.Floor(maximumSteps);
            var difference = maximumSteps - maximumStepsRoundedDown;

            for (var i = 0; i < maximumStepsRoundedDown; i++)
            {
                fmuInstance.AdvanceTime(fmuModel.SimulationFidelitySeconds);
            }

            // Advance the remainder of time to stay true to the simulation duration.
            fmuInstance.AdvanceTime(difference);

            AssignPropertyCacheCopyValues(fmuInstance, simulation.PropertyCache!, model.Variables);
        }

        private void AssignSimulationInputsToParameters(IModel model, IInstance fmuInstance, IEnumerable<(string, string, object)> fmuInputs) {
            foreach (var input in fmuInputs) {
                // We filter inputs by those accepted by the actual FMU.
                // TODO: figure out if we should do this outside of this loop here.
                if (model.Variables.TryGetValue(input.Item1, out var fmuVariable)){
                    var valueHandler = _factory.GetValueHandlerImplementation(input.Item2);
                    valueHandler.WriteValueToSimulationParameter(fmuInstance, fmuVariable, input.Item3);
                } else {
                    _logger.LogInformation("FMU variable {variable} not relevant.", input.Item1);
                }
            }
        }

        private void AssignPropertyCacheCopyValues(IInstance fmuInstance, PropertyCache propertyCacheCopy, IReadOnlyDictionary<string, IVariable> fmuOutputs)
        {
            // Find the correct Property from the simpler output variable name and assign its value.
            var logMsg = "";
            foreach (var fmuOutput in fmuOutputs)
            {
                foreach (var propertyName in propertyCacheCopy.Properties.Keys.Where(propertyName => propertyName.EndsWith($"#{fmuOutput.Key}")))
                {
                        var valueHandler = _factory.GetValueHandlerImplementation(propertyCacheCopy.Properties[propertyName].OwlType);
                        var value = valueHandler.GetValueFromSimulationParameter(fmuInstance, fmuOutput.Value);

                        logMsg += $"New value for {propertyName}: {value}\n";
                        propertyCacheCopy.Properties[propertyName].Value = value;
                }
            }
            _logger.LogInformation(logMsg);   
        }

        private List<OptimalCondition> GetAllOptimalConditions(PropertyCache propertyCache) {
            var optimalConditions = new List<OptimalCondition>();

            var query = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT ?optimalCondition ?property ?reachedInMaximumSeconds WHERE {
                ?optimalCondition rdf:type meta:OptimalCondition .
                ?optimalCondition ssn:forProperty ?property .
                ?optimalCondition meta:reachedInMaximumSeconds ?reachedInMaximumSeconds . }");

            var queryResult = _mapekKnowledge.ExecuteQuery(query);

            // For each OptimalCondition, process its respective constraints and get the appropriate Actions
            // for mitigation.
            foreach (var result in queryResult.Results) {
                var optimalConditionNode = result["optimalCondition"];
                var propertyNode = result["property"];
                var propertyName = propertyNode.ToString();

                Property property = null!;

                if (propertyCache.Properties.ContainsKey(propertyName)) {
                    property = propertyCache.Properties[propertyName];
                } else if (propertyCache.ConfigurableParameters.ContainsKey(propertyName)) {
                    property = propertyCache.ConfigurableParameters[propertyName];
                } else {
                    throw new Exception($"Property {propertyName} not found in the property cache.");
                }

                var reachedInMaximumSeconds = result["reachedInMaximumSeconds"];
                var reachedInMaximumSecondsValue = reachedInMaximumSeconds.ToString().Split('^')[0];

                // Build this OptimalCondition's full expression tree.
                var constraints = GetOptimalConditionConstraints(optimalConditionNode, propertyNode, reachedInMaximumSeconds) ??
                    throw new Exception($"OptimalCondition {optimalConditionNode.ToString()} has no constraints.");

                var optimalCondition = new OptimalCondition() {
                    Constraints = constraints,
                    ConstraintValueType = property.OwlType,
                    Name = optimalConditionNode.ToString(),
                    Property = propertyName,
                    ReachedInMaximumSeconds = int.Parse(reachedInMaximumSecondsValue),
                    UnsatisfiedAtomicConstraints = []
                };

                optimalConditions.Add(optimalCondition);
            }

            return optimalConditions;
        }

        private List<ConstraintExpression> GetOptimalConditionConstraints(INode optimalCondition, INode property, INode reachedInMaximumSeconds) {
            var constraintExpressions = new List<ConstraintExpression>();

            // This could be made more streamlined and elegant through the use of fewer, more cleverly combined queries, however,
            // SPARQL doesn't handle bNode identities, so these can't be used as variables for later referencing. For this reason, it's
            // necessary to loop through all range constraint operators (">", ">=", "<", "<=") to execute the same queries with each one.
            //
            // Documentation: (https://www.w3.org/TR/sparql11-query/#BlankNodesInResults)
            // "An application writer should not expect blank node labels in a query to refer to a particular blank node in the data."
            // For this reason, queries can be constructed with contiguous chains of bNodes, however, saving their INode objects and
            // using them as variables in subsequent queries doesn't work.
            //
            // A workaround would certainly be to insert triples as markings (much like for the inference rules), but the instance
            // model should probably not be polluted in light of other options.
            var operatorFilters = new List<ConstraintType>
            {
                ConstraintType.GreaterThan,
                ConstraintType.GreaterThanOrEqualTo,
                ConstraintType.LessThan,
                ConstraintType.LessThanOrEqualTo
            };

            AddConstraintsOfFirstOrOnlyRangeValues(constraintExpressions,
                optimalCondition,
                property,
                reachedInMaximumSeconds,
                operatorFilters);

            AddConstraintsOfSecondRangeValues(constraintExpressions,
                optimalCondition,
                property,
                reachedInMaximumSeconds,
                operatorFilters);

            AddConstraintsOfDisjunctionsOfOneAndOne(constraintExpressions,
                optimalCondition,
                property,
                reachedInMaximumSeconds,
                operatorFilters);

            // For models created with Protege (and the OWL API), disjunctions containing 1 and then 2 values will be converted to those
            // containing 2 and then 1.
            AddConstraintsOfDisjunctionsOfTwoAndOne(constraintExpressions,
                optimalCondition,
                property,
                reachedInMaximumSeconds,
                operatorFilters);

            AddConstraintsOfDisjunctionsOfTwoAndTwo(constraintExpressions,
                optimalCondition,
                property,
                reachedInMaximumSeconds,
                operatorFilters);

            return constraintExpressions;
        }

        private void AddConstraintsOfFirstOrOnlyRangeValues(IList<ConstraintExpression> constraintExpressions,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            IEnumerable<ConstraintType> constraintTypes) {
            foreach (var constraintType in constraintTypes) {
                var operatorFilter = GetOperatorFilterFromConstraintType(constraintType);

                // Gets the constraints of first or only values of ranges.
                var query = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 .
                    ?bNode3 rdf:first [ " + operatorFilter + " ?constraint ] .}");

                query.SetParameter("optimalCondition", optimalCondition);
                query.SetParameter("property", property);
                query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

                var queryResult = _mapekKnowledge.ExecuteQuery(query);

                foreach (var result in queryResult.Results) {
                    var constraint = result["constraint"].ToString().Split('^')[0];

                    var constraintExpression = new AtomicConstraintExpression {
                        Right = constraint,
                        ConstraintType = constraintType
                    };

                    constraintExpressions.Add(constraintExpression);
                }
            }
        }

        private void AddConstraintsOfSecondRangeValues(IList<ConstraintExpression> constraintExpressions,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            IEnumerable<ConstraintType> constraintTypes) {
            foreach (var constraintType in constraintTypes) {
                var operatorFilter = GetOperatorFilterFromConstraintType(constraintType);

                // Gets the constraints of the second values of ranges.
                var query = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 .
                    ?bNode3 rdf:rest ?bNode4 .
                    ?bNode4 rdf:first [ " + operatorFilter + " ?constraint ] . }");

                query.SetParameter("optimalCondition", optimalCondition);
                query.SetParameter("property", property);
                query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

                var queryResult = _mapekKnowledge.ExecuteQuery(query);

                foreach (var result in queryResult.Results) {
                    var constraint = result["constraint"].ToString().Split('^')[0];

                    var constraintExpression = new AtomicConstraintExpression {
                        Right = constraint,
                        ConstraintType = constraintType
                    };

                    constraintExpressions.Add(constraintExpression);
                }
            }
        }

        private void AddConstraintsOfDisjunctionsOfOneAndOne(IList<ConstraintExpression> constraintExpressions,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            IEnumerable<ConstraintType> constraintTypes) {
            foreach (var constraintType1 in constraintTypes) {
                var operatorFilter1 = GetOperatorFilterFromConstraintType(constraintType1);

                foreach (var constraintType2 in constraintTypes) {
                    var operatorFilter2 = GetOperatorFilterFromConstraintType(constraintType2);

                    // Gets the constraints of two disjunctive, single-valued ranges.
                    var query = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT ?constraint1 ?constraint2 WHERE {
                        @optimalCondition ssn:forProperty @property .
                        @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                        @optimalCondition rdf:type ?bNode1 .
                        ?bNode1 owl:onProperty meta:hasValueConstraint .
                        ?bNode1 owl:onDataRange ?bNode2 .
                        ?bNode2 owl:unionOf ?bNode3 .
                        ?bNode3 rdf:first ?bNode4_1 .
                        ?bNode4_1 owl:withRestrictions ?bNode5_1 .
                        ?bNode5_1 rdf:first [ " + operatorFilter1 + @" ?constraint1 ] .
                        ?bNode5_1 rdf:rest () .
                        ?bNode3 rdf:rest ?bNode4_2 .
                        ?bNode4_2 rdf:first ?bNode5_2 .
                        ?bNode5_2 owl:withRestrictions ?bNode6_2 .
                        ?bNode6_2 rdf:first [ " + operatorFilter2 + @" ?constraint2 ] .
                        ?bNode6_2 rdf:rest () . }");

                    query.SetParameter("optimalCondition", optimalCondition);
                    query.SetParameter("property", property);
                    query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

                    var queryResult = _mapekKnowledge.ExecuteQuery(query);

                    foreach (var result in queryResult.Results) {
                        var leftConstraint = result["constraint1"].ToString().Split('^')[0];
                        var rightConstraint = result["constraint2"].ToString().Split('^')[0];

                        var leftConstraintExpression = new AtomicConstraintExpression {
                            Right = leftConstraint,
                            ConstraintType = constraintType1
                        };
                        var rightConstraintExpression = new AtomicConstraintExpression {
                            Right = rightConstraint,
                            ConstraintType = constraintType2
                        };
                        var disjunctiveExpression = new NestedConstraintExpression {
                            Left = leftConstraintExpression,
                            Right = rightConstraintExpression,
                            ConstraintType = ConstraintType.Or
                        };

                        constraintExpressions.Add(disjunctiveExpression);
                    }
                }
            }
        }

        private void AddConstraintsOfDisjunctionsOfTwoAndOne(IList<ConstraintExpression> constraintExpressions,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            IEnumerable<ConstraintType> constraintTypes) {
            foreach (var constraintType1 in constraintTypes) {
                var operatorFilter1 = GetOperatorFilterFromConstraintType(constraintType1);

                foreach (var constraintType2 in constraintTypes) {
                    var operatorFilter2 = GetOperatorFilterFromConstraintType(constraintType2);

                    foreach (var constraintType3 in constraintTypes) {
                        var operatorFilter3 = GetOperatorFilterFromConstraintType(constraintType3);

                        // Gets the constraints of two disjunctive ranges, one two-valued and the other single-valued.
                        var query = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT ?constraint1 ?constraint2 ?constraint3 WHERE {
                            @optimalCondition ssn:forProperty @property .
                            @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                            @optimalCondition rdf:type ?bNode1 .
                            ?bNode1 owl:onProperty meta:hasValueConstraint .
                            ?bNode1 owl:onDataRange ?bNode2 .
                            ?bNode2 owl:unionOf ?bNode3 .
                            ?bNode3 rdf:first ?bNode4_1 .
                            ?bNode4_1 owl:withRestrictions ?bNode5_1 .
                            ?bNode5_1 rdf:first [ " + operatorFilter1 + @" ?constraint1 ] .
                            ?bNode5_1 rdf:rest ?bNode6_1 .
                            ?bNode6_1 rdf:first [ " + operatorFilter2 + @" ?constraint2 ] .
                            ?bNode3 rdf:rest ?bNode4_2 .
                            ?bNode4_2 rdf:first ?bNode5_2 .
                            ?bNode5_2 owl:withRestrictions ?bNode6_2 .
                            ?bNode6_2 rdf:first [ " + operatorFilter3 + @" ?constraint3 ] .
                            ?bNode6_2 rdf:rest () . }");

                        query.SetParameter("optimalCondition", optimalCondition);
                        query.SetParameter("property", property);
                        query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

                        var queryResult = _mapekKnowledge.ExecuteQuery(query);

                        foreach (var result in queryResult.Results) {
                            var leftConstraint1 = result["constraint1"].ToString().Split('^')[0];
                            var leftConstraint2 = result["constraint2"].ToString().Split('^')[0];
                            var rightConstraint = result["constraint3"].ToString().Split('^')[0];

                            var leftConstraintExpression1 = new AtomicConstraintExpression {
                                Right = leftConstraint1,
                                ConstraintType = constraintType1
                            };
                            var leftConstraintExpression2 = new AtomicConstraintExpression {
                                Right = leftConstraint2,
                                ConstraintType = constraintType2
                            };
                            var rightConstraintExpression = new AtomicConstraintExpression {
                                Right = rightConstraint,
                                ConstraintType = constraintType3
                            };
                            var leftConstraintExpression = new NestedConstraintExpression {
                                Left = leftConstraintExpression1,
                                Right = leftConstraintExpression2,
                                ConstraintType = ConstraintType.And
                            };
                            var disjunctiveExpression = new NestedConstraintExpression {
                                Left = leftConstraintExpression,
                                Right = rightConstraintExpression,
                                ConstraintType = ConstraintType.Or
                            };

                            constraintExpressions.Add(disjunctiveExpression);
                        }
                    }
                }
            }
        }

        private void AddConstraintsOfDisjunctionsOfTwoAndTwo(IList<ConstraintExpression> constraintExpressions,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            IEnumerable<ConstraintType> constraintTypes) {
            foreach (var constraintType1 in constraintTypes) {
                var operatorFilter1 = GetOperatorFilterFromConstraintType(constraintType1);

                foreach (var constraintType2 in constraintTypes) {
                    var operatorFilter2 = GetOperatorFilterFromConstraintType(constraintType2);

                    foreach (var constraintType3 in constraintTypes) {
                        var operatorFilter3 = GetOperatorFilterFromConstraintType(constraintType3);

                        foreach (var constraintType4 in constraintTypes) {
                            var operatorFilter4 = GetOperatorFilterFromConstraintType(constraintType4);

                            // Gets the constraints of two disjunctive, two-valued ranges.
                            var query = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT ?constraint1 ?constraint2 ?constraint3 ?constraint4 WHERE {
                                @optimalCondition ssn:forProperty @property .
                                @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                                @optimalCondition rdf:type ?bNode1 .
                                ?bNode1 owl:onProperty meta:hasValueConstraint .
                                ?bNode1 owl:onDataRange ?bNode2 .
                                ?bNode2 owl:unionOf ?bNode3 .
                                ?bNode3 rdf:first ?bNode4_1 .
                                ?bNode4_1 owl:withRestrictions ?bNode5_1 .
                                ?bNode5_1 rdf:first [ " + operatorFilter1 + @" ?constraint1 ] .
                                ?bNode5_1 rdf:rest ?bNode6_1 .
                                ?bNode6_1 rdf:first [ " + operatorFilter2 + @" ?constraint2 ] .
                                ?bNode3 rdf:rest ?bNode4_2 .
                                ?bNode4_2 rdf:first ?bNode5_2 .
                                ?bNode5_2 owl:withRestrictions ?bNode6_2 .
                                ?bNode6_2 rdf:first [ " + operatorFilter3 + @" ?constraint3 ] .
                                ?bNode6_2 rdf:rest ?bNode7_2 .
                                ?bNode7_2 rdf:first [ " + operatorFilter4 + " ?constraint4 ] . }");

                            query.SetParameter("optimalCondition", optimalCondition);
                            query.SetParameter("property", property);
                            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

                            var queryResult = _mapekKnowledge.ExecuteQuery(query);

                            foreach (var result in queryResult.Results) {
                                var leftConstraint1 = result["constraint1"].ToString().Split('^')[0];
                                var leftConstraint2 = result["constraint2"].ToString().Split('^')[0];
                                var rightConstraint1 = result["constraint3"].ToString().Split('^')[0];
                                var rightConstraint2 = result["constraint4"].ToString().Split('^')[0];

                                var leftConstraintExpression1 = new AtomicConstraintExpression {
                                    Right = leftConstraint1,
                                    ConstraintType = constraintType1
                                };
                                var leftConstraintExpression2 = new AtomicConstraintExpression {
                                    Right = leftConstraint2,
                                    ConstraintType = constraintType2
                                };
                                var rightConstraintExpression1 = new AtomicConstraintExpression {
                                    Right = rightConstraint1,
                                    ConstraintType = constraintType3
                                };
                                var rightConstraintExpression2 = new AtomicConstraintExpression {
                                    Right = rightConstraint2,
                                    ConstraintType = constraintType4
                                };
                                var leftConstraintExpression = new NestedConstraintExpression {
                                    Left = leftConstraintExpression1,
                                    Right = leftConstraintExpression2,
                                    ConstraintType = ConstraintType.And
                                };
                                var rightConstraintExpression = new NestedConstraintExpression {
                                    Left = rightConstraintExpression1,
                                    Right = rightConstraintExpression2,
                                    ConstraintType = ConstraintType.And
                                };
                                var disjunctiveExpression = new NestedConstraintExpression {
                                    Left = leftConstraintExpression,
                                    Right = rightConstraintExpression,
                                    ConstraintType = ConstraintType.Or
                                };

                                constraintExpressions.Add(disjunctiveExpression);
                            }
                        }
                    }
                }
            }
        }

        private static string GetOperatorFilterFromConstraintType(ConstraintType constraintType) {
            return constraintType switch {
                ConstraintType.GreaterThan => "xsd:minExclusive",
                ConstraintType.GreaterThanOrEqualTo => "xsd:minInclusive",
                ConstraintType.LessThan => "xsd:maxExclusive",
                ConstraintType.LessThanOrEqualTo => "xsd:maxInclusive",
                _ => throw new Exception($"{constraintType} is an invalid comparison operator.")
            };
        }

        private int GetNumberOfSatisfiedOptimalConditions(IEnumerable<OptimalCondition> optimalConditions, PropertyCache propertyCache)
        {
            var numberOfSatisfiedOptimalConditions = 0;

            foreach (var optimalCondition in optimalConditions)
            {
                var valueHandler = _factory.GetValueHandlerImplementation(optimalCondition.ConstraintValueType);

                object propertyValue;

                if (propertyCache.ConfigurableParameters.TryGetValue(optimalCondition.Property, out ConfigurableParameter? configurableParameter))
                {
                    propertyValue = configurableParameter.Value;
                }
                else if (propertyCache.Properties.TryGetValue(optimalCondition.Property, out Property? property))
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

                    if (!unsatisfiedConstraints.Any())
                    {
                        numberOfSatisfiedOptimalConditions++;
                    }
                }
            }

            return numberOfSatisfiedOptimalConditions;
        }

        private SimulationPath GetOptimalSimulationPath(PropertyCache propertyCache,
            IEnumerable<OptimalCondition> optimalConditions,
            IEnumerable<SimulationPath> simulationPaths)
        {
            // This method is a filter for finding the optimal simulation path. It works in a few steps of descending precedance, each of which further reduces the set of
            // simulation paths:
            // 1. Filter for simulation paths that satisfy the most OptimalConditions.
            // 2. Filter for simulation paths that have the highest number of the most optimized Properties.
            // 3. Pick the first one.

            if (!simulationPaths.Any())
            {
                return null!;
            }

            // Filter for simulation configurations that satisfy the most OptimalConditions.
            var simulationPathsWithMostOptimalConditionsSatisfied = GetSimulationPathsWithMostOptimalConditionsSatisfied(simulationPaths,
                optimalConditions);

            if (simulationPathsWithMostOptimalConditionsSatisfied.Count == 1)
            {
                return simulationPathsWithMostOptimalConditionsSatisfied.First();
            }

            _logger.LogInformation("{count} simulation configurations remaining after the first filter.", simulationPathsWithMostOptimalConditionsSatisfied.Count);

            // Filter for simulation configurations that optimize the most targeted Properties.
            var simulationPathsWithMostOptimizedProperties =
                GetSimulationPathsWithMostOptimizedProperties(simulationPathsWithMostOptimalConditionsSatisfied,
                    propertyCache);

            _logger.LogInformation("{count} simulation configurations remaining after the second filter.", simulationPathsWithMostOptimizedProperties.Count);

            // At this point, arbitrarily return the first one regardless of the number of simulation configurations remaining.
            return simulationPathsWithMostOptimizedProperties.First();
        }

        private List<SimulationPath> GetSimulationPathsWithMostOptimalConditionsSatisfied(IEnumerable<SimulationPath> simulationPaths,
            IEnumerable<OptimalCondition> optimalConditions)
        {
            var passingSimulationPaths = new List<SimulationPath>();
            var highestNumberOfSatisfiedOptimalConditions = 0;

            foreach (var simulationPath in simulationPaths)
            {
                var numberOfSatisfiedOptimalConditions = GetNumberOfSatisfiedOptimalConditions(optimalConditions, simulationPath.Simulations.Last().PropertyCache!);

                if (numberOfSatisfiedOptimalConditions > highestNumberOfSatisfiedOptimalConditions)
                {
                    highestNumberOfSatisfiedOptimalConditions = numberOfSatisfiedOptimalConditions;
                    passingSimulationPaths = [simulationPath];
                }
                else if (numberOfSatisfiedOptimalConditions == highestNumberOfSatisfiedOptimalConditions)
                {
                    passingSimulationPaths.Add(simulationPath);
                }
            }

            return passingSimulationPaths;
        }

        private List<SimulationPath> GetSimulationPathsWithMostOptimizedProperties(IEnumerable<SimulationPath> simulationPaths,
            PropertyCache propertyCache)
        {
            var propertyChangesToOptimizeFor = GetPropertyChangesToOptimizeFor(propertyCache);
            var valueHandlers = propertyChangesToOptimizeFor.Select(p => _factory.GetValueHandlerImplementation(p.Property.OwlType));

            _logger.LogInformation("Ordering and filtering simulation results...");
            
            var simulationPathComparer = new SimulationPathComparer(propertyChangesToOptimizeFor.Zip(valueHandlers));

            // Return the simulation configurations with the maximum score.
            return simulationPaths.OrderByDescending(s => s, simulationPathComparer)
                .Where(s => simulationPathComparer.Compare(s, simulationPaths.First()) > -1)
                .ToList();
        }

        private List<PropertyChange> GetPropertyChangesToOptimizeFor(PropertyCache propertyCache)
        {
            var propertyChangesToOptimizeFor = new List<PropertyChange>();

            var query = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT ?propertyChange ?property ?effect WHERE {
                ?platform rdf:type sosa:Platform .
                ?platform meta:optimizesFor ?propertyChange .
                ?propertyChange ssn:forProperty ?property .
                ?propertyChange meta:affectsPropertyWith ?effect . }");

            var queryResult = _mapekKnowledge.ExecuteQuery(query);

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

        private void LogOptimalSimulationPath(SimulationPath optimalSimulationPath)
        {
            var logMsg = "Chosen optimal path, Actuation actions:\n";

            // Convert to a list to use indexing.
            var simulationList = optimalSimulationPath.Simulations.ToList();

            for (var i = 0; i < simulationList.Count; i++)
            {
                logMsg += $"Interval {i + 1}:\n";

                foreach (var action in simulationList[i].Actions)
                {
                    if (action is ActuationAction actuationAction) {
                        logMsg += $"Actuator: {actuationAction.Actuator.Name}, Actuator state: {actuationAction.NewStateValue.ToString()}.\n";
                    } else {
                        var reconfigurationAction = (ReconfigurationAction)action;
                        logMsg += $"ConfigurableParameter: {reconfigurationAction.ConfigurableParameter.Name}, ConfigurableParameter value: {reconfigurationAction.NewParameterValue.ToString()}.\n";
                    }
                }
            }

            _logger.LogInformation(logMsg);
        }
    }
}
