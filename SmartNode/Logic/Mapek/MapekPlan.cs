using Femyou;
using Fitness;
using Logic.FactoryInterface;
using Logic.Mapek.Comparers;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.Diagnostics;
using static Femyou.IModel;

namespace Logic.Mapek
{
    public class MapekPlan : IMapekPlan, IDisposable 
    {
        // Required as fields to preserve caching throughout multiple MAPE-K loop cycles.
        private readonly Dictionary<string, IModel> _fmuDict = [];
	    private readonly Dictionary<string, IInstance> _iDict = [];
        // A setting that determines whether the DT operates in the 'reactive' (true) or 'proactive' (false) mode. The reactive mode generates only those Actions that will
        // act as mitigations against violated OptimalConditions. The proactive mode generates all possible Actions from all Actuators and/or ConfigurableParameters in the
        // instance model.
        private readonly bool _restrictToReactiveActionsOnly;
        private bool _restrictToReactiveActionsOnlyOld; // Used for performance enhancements.
        private bool _javaInvocationAsyncError = false; // Used to track async errors from Java invocation.

        private bool _savedReactiveSetting = false;

        private readonly CoordinatorSettings _coordinatorSettings;
        private readonly ILogger<IMapekPlan> _logger;
        private readonly IFactory _factory;
        private readonly IMapekKnowledge _mapekKnowledge;
        private readonly FilepathArguments _filepathArguments;

        public MapekPlan(IServiceProvider serviceProvider)
        {
            _coordinatorSettings = serviceProvider.GetRequiredService<CoordinatorSettings>();
            _restrictToReactiveActionsOnly = _coordinatorSettings.StartInReactiveMode;
            _restrictToReactiveActionsOnlyOld = _restrictToReactiveActionsOnly;
            _logger = serviceProvider.GetRequiredService<ILogger<IMapekPlan>>();
            _factory = serviceProvider.GetRequiredService<IFactory>();
            _mapekKnowledge = serviceProvider.GetRequiredService<IMapekKnowledge>();
            _filepathArguments = serviceProvider.GetRequiredService<FilepathArguments>();
        }

        /// <summary>
        /// Produces a plan of execution for the next MAPE-K phase by simulating hypothetical scenarios according to system configuration.
        /// </summary>
        /// <param name="cache">The knowledge base cache consisting of Properties, Actuators, OptimalConditions, and a tree of soft Sensors for a preserved order in their execution.</param>
        /// <returns>
        /// A (task of a) tuple of a tree of simulations and an optimal decision represented as a path from level 1 (level after root) to leaf on that tree. Every node in the simulation tree
        /// represents a unique decision at a given cycle (tree level). The root node represents the current cycle and contains the originally observed Property values. The decision will contains
        /// a sequence of simulations with each containing the actions to take and their predicted results.
        /// </returns>
        public async Task<(SimulationTreeNode, SimulationPath)> Plan(Cache cache) {
            _logger.LogInformation("Starting the Plan phase.");

            if (_coordinatorSettings.UseDecisionLagMitigation) {
                _logger.LogInformation("Decision lag mitigation active. Estimating real-world simulation duration.");

                cache.PropertyCache = await GetDecisionLagMitigationPropertyCache(_coordinatorSettings.LookAheadMapekCycles, cache.Actuators, cache.PropertyCache, cache.SoftSensorTreeNodes);
            }

            _logger.LogInformation("Generating simulations.");

            // Get all combinations of possible simulation configurations for the given number of cycles.
            var simulationTree = new SimulationTreeNode {
                NodeItem = new Simulation(cache.PropertyCache),
                Children = []
            };
            var simulations = GetSimulationsAndGenerateSimulationTree(_coordinatorSettings.LookAheadMapekCycles, 0, simulationTree, false, true, new List<List<ActuationAction>>(), cache.PropertyCache);

            // Execute the simulations and obtain their results.
            await Simulate(simulations, cache.SoftSensorTreeNodes);

            _logger.LogInformation("Generated a total of {total} simulation paths.", simulationTree.SimulationPaths.Count());

            // Find the optimal simulation path.
            var optimalSimulationPath = GetOptimalSimulationPath(cache, simulationTree.SimulationPaths);

            LogOptimalSimulationPath(optimalSimulationPath.First());

            return (simulationTree, optimalSimulationPath!.First());
        }

        // Estimates how long simulation will take in real-world time to more accurately predict how the observed conditions change between data observation and decision execution.
        // It uses the full number of Actuators and their states to make a 'worst-case scenario' prediction (proactive mode). Reactive mode is difficult to accomplish without simulating.
        // This currently only supports physical TT components (Actuators through ActuationActions).
        private async Task<PropertyCache> GetDecisionLagMitigationPropertyCache(int lookAheadCycles,
            IDictionary<string, Actuator> actuatorCache,
            PropertyCache propertyCache,
            IEnumerable<SoftSensorTreeNode> softSensorTreeNodes) {
            var fakeSimulationsToRunForAverageDuration = 100;

            // Get the total number of simulations (proactive mode).
            var numberOfSimulations = GetNumberOfSimulations(lookAheadCycles);

            // Construct fake ActuationActions for the fake simulations.
            var fakeActuationActions = new List<ActuationAction>();
            foreach (var actuatorKeyValue in actuatorCache) {
                fakeActuationActions.Add(new ActuationAction {
                    Actuator = actuatorKeyValue.Value,
                    Name = actuatorKeyValue.Key + actuatorKeyValue.Value.State!.ToString(),
                    NewStateValue = actuatorKeyValue.Value.State
                });
            }

            // Set up the parameters.
            var fmuModels = GetHostPlatformFmuModel(_filepathArguments.FmuDirectory);
            var simulation = new Simulation(propertyCache) {
                Index = 0,
                Actions = fakeActuationActions
            };

            // Measure time.
            Stopwatch stopwatch = new();
            stopwatch.Start();
            for (var i = 0; i < fakeSimulationsToRunForAverageDuration; i++) {
                Debug.Assert(fmuModels.Count() == 1);
                ExecuteFmu(fmuModels.First(), simulation, simulation.PropertyCache); // XXX
                await ExecuteSoftSensorsAndUpdateSimulationCache(simulation, softSensorTreeNodes);
            }
            stopwatch.Stop();

            // Calculate the estimated duration for the total number of simulations.
            var estimatedRealWorldSimulationDurationSeconds = stopwatch.ElapsedMilliseconds / fakeSimulationsToRunForAverageDuration / 1000.0 * numberOfSimulations;

            // Reset the simulation to use the original PropertyCache.
            simulation = new Simulation(propertyCache) {
                Index = 0,
                Actions = fakeActuationActions
            };

            // Execute the decision lag mitigation simulation to get the predicted Property values at the time of decision execution.
            ExecuteFmu(fmuModels.First() /* XXX */, simulation, null, estimatedRealWorldSimulationDurationSeconds);

            return simulation.PropertyCache;
        }

        private int GetNumberOfSimulations(int lookAheadCycles) {
            // Get counts of Actuator states grouped by Actuator.
            var query = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT ?actuator (COUNT(?actuatorState) as ?actuatorStateCount) WHERE {
                ?actuator rdf:type sosa:Actuator .
                ?actuator meta:hasActuatorState ?actuatorState. }
                GROUP BY ?actuator");

            var queryResult = _mapekKnowledge.ExecuteQuery(query);

            // Calculate the total number of unique combinations.
            var totalCombinations = 1.0;
            foreach (var result in queryResult.Results) {
                var actuatorStateCountString = result["actuatorStateCount"].ToString().Split("^")[0];
                var actuatorStateCount = int.Parse(actuatorStateCountString);

                totalCombinations *= actuatorStateCount;
            }

            // Calculate the total number of simulations.
            var totalSimulations = 0.0;
            for (var i = lookAheadCycles; i > 0; i--) {
                totalSimulations += Math.Pow(totalCombinations, i);
            }

            return (int)totalSimulations;
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
                var simulation = new Simulation(GetPropertyCacheCopy(simulationTreeNode.NodeItem.PropertyCache)) {
                    Actions = actionCombination.Where(action => action is not FMUParameterAction),
                    InitializationActions = actionCombination.Where(action => action is FMUParameterAction).Select(a => (FMUParameterAction)a),
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
                        simulation.PropertyCache);

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
            // Write the setting at least once to disk. Only write again if the setting changes.
            if (_savedReactiveSetting) {
                if (_restrictToReactiveActionsOnly == _restrictToReactiveActionsOnlyOld) {
                    return;
                }
            } else {
                _savedReactiveSetting = true;
            }

            _restrictToReactiveActionsOnlyOld = _restrictToReactiveActionsOnly;

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
                if (e.Data != null && e.Data.Contains("Error!")) {
                    SetError(); // Async was here
                }
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

            Debug.Assert(!_javaInvocationAsyncError, "Inconsistencies detected.");
            _logger.LogInformation("Process {processId} exited with code {processExitCode}.", process.Id, process.ExitCode);
        }

        private void SetError() {
            _javaInvocationAsyncError = true;
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

        internal static IEnumerable<IEnumerable<T>> GetNaryCartesianProducts<T> (IEnumerable<IEnumerable<T>> sequences)
        {
            IEnumerable<IEnumerable<T>> emptyProduct = new[] { Enumerable.Empty<T>() };
            return sequences.Aggregate(
              emptyProduct,
              (accumulator, sequence) =>
                from accseq in accumulator
                from item in sequence
                select accseq.Concat(new[] { item }));
        }

        internal async Task Simulate(IEnumerable<Simulation> simulations, IEnumerable<SoftSensorTreeNode> softSensorTreeNodes)
        {
            // Retrieve the host platform FMU and its simulation fidelity for ActuationAction simulations.
            var fmuModels = GetHostPlatformFmuModel(_filepathArguments.FmuDirectory);

            // Measure simulation time.
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            int i = 0;
            // TODO: Parallelize simulations (#13).
            foreach (var simulation in simulations) {
                _logger.LogInformation("Running simulation #{run}", i++);

                var orig = new Simulation(GetPropertyCacheCopy(simulation.PropertyCache!)) {
                        Actions = simulation.Actions,
                        InitializationActions = simulation.InitializationActions,
                        Index = simulation.Index
                    };
                // Perform the simulation via FMU execution and ensure all the Properties in the simulation's property cache are updated by running all soft sensors
                // in the correct order.
                foreach (var fmuModel in fmuModels) {
                    // Run each FMU with the initial state
                    var s = new Simulation(GetPropertyCacheCopy(simulation.PropertyCache!)) {
                        Actions = simulation.Actions,
                        InitializationActions = simulation.InitializationActions,
                        Index = simulation.Index
                    };
                    // ... and merge FMU outputs:
                    // TODO: Assert non-overlapping outputs!
                    ExecuteFmu(fmuModel, s, simulation.PropertyCache);
                }

                await ExecuteSoftSensorsAndUpdateSimulationCache(simulation, softSensorTreeNodes);
                if (GetFitnessOps() != null) {
                    Fitness.Fitness fitness = new(orig) {
                        FOps = GetFitnessOps().ToArray()
                    };
                    // Sideeffect:
                    fitness.Process(fitness.MkState(), simulation);
                }
            }

            stopwatch.Stop();
            _logger.LogInformation("Total simulation time (seconds): {elapsedTime}", (double)stopwatch.ElapsedMilliseconds / 1000);
        }

        public virtual IEnumerable<FOp> GetFitnessOps()
        {
            return [];
        }

        private async static Task ExecuteSoftSensorsAndUpdateSimulationCache(Simulation simulation, IEnumerable<SoftSensorTreeNode> softSensorTreeNodes) {
            // Execute the tree of soft sensors in the correct order to ensure all Properties in the simulation's property cache are updated.
            foreach (var softSensorTreeNode in softSensorTreeNodes) {
                if (softSensorTreeNode.Children.Any()) {
                    await ExecuteSoftSensorsAndUpdateSimulationCache(simulation, softSensorTreeNode.Children);

                    var inputs = new List<object>();
                    foreach (var softSensorTreeNodeChild in softSensorTreeNode.Children) {
                        var inputProperty = simulation.PropertyCache!.Properties[softSensorTreeNodeChild.OutputProperty];
                        inputs.Add(inputProperty.Value);
                    }

                    var propertyValue = await softSensorTreeNode.NodeItem.ObservePropertyValue(inputs.ToArray());
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

            VDS.RDF.Query.SparqlResultSet queryResult = GetStaticObservables();

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

        VDS.RDF.Query.SparqlResultSet staticObservables = null;
        private VDS.RDF.Query.SparqlResultSet GetStaticObservables() {
            if (staticObservables != null) {
                return staticObservables;
            }
            var query = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT DISTINCT ?observableProperty WHERE {
                ?sensor rdf:type sosa:Sensor .
                ?sensor sosa:observes ?observableProperty . }");

            staticObservables = _mapekKnowledge.ExecuteQuery(query);
            return staticObservables;
        }

        private IEnumerable<FmuModel> GetHostPlatformFmuModel(string fmuDirectory) {
            // Retrieve the Platform (TT) FMU to be used for Actuators and/or ConfigurableParameters.
            var query = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT ?fmuModel ?fmuFilePath ?simulationFidelitySeconds WHERE {
                ?platform rdf:type sosa:Platform .
                ?platform meta:hasSimulationModel ?fmuModel .
                ?fmuModel rdf:type meta:FmuModel .
                ?fmuModel meta:hasURI ?fmuFilePath .
                ?fmuModel meta:hasSimulationFidelitySeconds ?simulationFidelitySeconds . }");

            var queryResult = _mapekKnowledge.ExecuteQuery(query);

            var result = new List<FmuModel>();
            foreach (var fmuModel in queryResult.Results) {

                result.Add(new FmuModel {
                    Name = fmuModel["fmuModel"].ToString(),
                    Filepath = Path.Combine(fmuDirectory, fmuModel["fmuFilePath"].ToString().Split('^')[0]),
                    SimulationFidelitySeconds = int.Parse(fmuModel["simulationFidelitySeconds"].ToString().Split('^')[0])
                });
            }
            return result;
        }

        // Initialize the FMU between enter/exitInitialization (#42).
        protected virtual bool Initialization(Simulation simulation, IModel model, IInstance fmuInstance) {
            var actions = simulation.InitializationActions.Select(action => (action.Actuator.ParameterName ?? MapekUtilities.GetSimpleName(action.Name), action.Actuator.Type!, action.NewStateValue)).ToList();
            AssignSimulationInputsToParameters(model, fmuInstance, actions);
            return true;
        }

        // In case an FMU does not contain those functions, they can be marked as unsupported here.
        // We're listing fmi2SetTime here as a) we don't need it, and b) the Python FMUs don't provide it.
        protected virtual Collection<UnsupportedFunctions> GetUnsupportedFMUFunctions() {
            return new Collection<UnsupportedFunctions>([UnsupportedFunctions.SetTime2]);
        }

        private void ExecuteFmu(FmuModel fmuModel, Simulation simulation, PropertyCache outCache, double simulationDurationSeconds = 0) {
            if (simulationDurationSeconds == 0) {
                simulationDurationSeconds = _coordinatorSettings.CycleDurationSeconds;
            }

            // The LogDebug calls here are primarily to keep an eye on crashes in the FMU which are otherwise a tad harder to track down.
            // _logger.LogInformation("Simulation {simulation}", simulation); // XXX Arg useless.
            if (!_fmuDict.TryGetValue(fmuModel.Filepath, out IModel? model)) {
                _logger.LogDebug("Loading Model {filePath}", fmuModel.Filepath);
                model = Model.Load(fmuModel.Filepath, GetUnsupportedFMUFunctions());
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
            _logger.LogDebug("Setting time {t}", simulation.Index * simulationDurationSeconds);
            fmuInstance.StartTime(simulation.Index * simulationDurationSeconds, (i) => Initialization(simulation, model, i));

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
                    name = actuationAction.Actuator.ParameterName ?? actuationAction.Actuator.Name;
                    type = actuationAction.Actuator.Type!;
                    value = actuationAction.NewStateValue;
                } else {
                    var reconfigurationAction = (ReconfigurationAction)action;
                    // TODO: override here as well?
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

            _logger.LogDebug("Tick ({fmuName}), {secs}s", fmuInstance.Name, simulationDurationSeconds);
            // Advance the FMU time for the duration of the simulation tick in steps of simulation fidelity.
            var maximumSteps = (double)simulationDurationSeconds / fmuModel.SimulationFidelitySeconds;
            var maximumStepsRoundedDown = (int)Math.Floor(maximumSteps);
            var difference = maximumSteps - maximumStepsRoundedDown;

            for (var i = 0; i < maximumStepsRoundedDown; i++)
            {
                fmuInstance.AdvanceTime(fmuModel.SimulationFidelitySeconds);
            }

            // Advance the remainder of time to stay true to the simulation duration.
            fmuInstance.AdvanceTime(difference);

            AssignPropertyCacheCopyValues(fmuInstance, outCache, model.Variables);
        }

        private void AssignSimulationInputsToParameters(IModel model, IInstance fmuInstance, IEnumerable<(string, string, object)> fmuInputs) {
            IEnumerable<String> ignoredVars = [];
            foreach (var input in fmuInputs) {
                // We filter inputs by those accepted by the actual FMU.
                // TODO: figure out if we should do this outside of this loop here.
                if (model.Variables.TryGetValue(input.Item1, out var fmuVariable)){
                    var valueHandler = _factory.GetValueHandlerImplementation(input.Item2);
                    valueHandler.WriteValueToSimulationParameter(fmuInstance, fmuVariable, input.Item3);
                } else {
                    // might want to remove this in the future:
                    ignoredVars = ignoredVars.Append(input.Item1);
                }
            }
            _logger.LogDebug("FMU variables not relevant: {variables}", ignoredVars);
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

                        logMsg += $"{propertyName}: {value}\n";
                        propertyCacheCopy.Properties[propertyName].Value = value;
                }
            }
            _logger.LogInformation("New values:\n{vals}", logMsg);   
        }

        public virtual IEnumerable<SimulationPath> GetOptimalSimulationPath(Cache cache,
            IEnumerable<SimulationPath> simulationPaths)
        {
            // This method is a filter for finding the optimal simulation path. It works in a few steps of descending precedance, each of which further reduces the set of
            // simulation paths:
            // 1. Filter for simulation paths that satisfy the most OptimalConditions.
            // 2. Filter for simulation paths that have the highest number of the most optimized (lowest/highest) Properties.
            // 3. Pick the first one.

            PropertyCache propertyCache = cache.PropertyCache;
            IEnumerable<OptimalCondition> optimalConditions = cache.OptimalConditions;

            if (!simulationPaths.Any()) {
                return null!;
            }

            // Filter for simulation configurations that satisfy the most OptimalConditions.
            var simulationPathsWithMostOptimalConditionsSatisfied = GetSimulationPathsWithMostOptimalConditionsSatisfied(simulationPaths,
                optimalConditions);

            _logger.LogInformation("{count} simulation configurations remaining after the first filter.", simulationPathsWithMostOptimalConditionsSatisfied.Count);
            if (simulationPathsWithMostOptimalConditionsSatisfied.Count == 1) {
                return simulationPathsWithMostOptimalConditionsSatisfied;
            }

            // Filter for simulation configurations that optimize the most targeted Properties.
            var simulationPathsWithMostOptimizedProperties = GetSimulationPathsWithMostOptimizedProperties(simulationPathsWithMostOptimalConditionsSatisfied);

            _logger.LogInformation("{count} simulation configurations remaining after the second filter.", simulationPathsWithMostOptimizedProperties.Count());

            return simulationPathsWithMostOptimizedProperties;
        }

        private List<SimulationPath> GetSimulationPathsWithMostOptimalConditionsSatisfied(IEnumerable<SimulationPath> simulationPaths,
            IEnumerable<OptimalCondition> optimalConditions) {
            var passingSimulationPaths = new List<SimulationPath>();
            var highestNumberOfSatisfiedOptimalConditions = 0;

            foreach (var simulationPath in simulationPaths) {
                var simulationSatisfiedOptimalConditions = 0;
                foreach (var simulation in simulationPath.Simulations) {
                    simulationSatisfiedOptimalConditions += GetNumberOfSatisfiedOptimalConditions(optimalConditions, simulation.PropertyCache);
                }

                if (simulationSatisfiedOptimalConditions > highestNumberOfSatisfiedOptimalConditions) {
                    highestNumberOfSatisfiedOptimalConditions = simulationSatisfiedOptimalConditions;
                    passingSimulationPaths = [simulationPath];
                } else if (simulationSatisfiedOptimalConditions == highestNumberOfSatisfiedOptimalConditions) {
                    passingSimulationPaths.Add(simulationPath);
                }
            }

            return passingSimulationPaths;
        }

        private int GetNumberOfSatisfiedOptimalConditions(IEnumerable<OptimalCondition> optimalConditions, PropertyCache propertyCache) {
            var numberOfSatisfiedOptimalConditions = 0;

            foreach (var optimalCondition in optimalConditions) {

                Property p;
                if (propertyCache.ConfigurableParameters.TryGetValue(optimalCondition.Property.Name, out ConfigurableParameter? configurableParameter)) {
                    p = configurableParameter;
                    // Sanity-check. Seems a bit odd that we should've forgotten where this was originally coming from?
                    Debug.Assert(!propertyCache.Properties.TryGetValue(optimalCondition.Property.Name, out Property? property), "This should probably not have happened.");
                } else if (propertyCache.Properties.TryGetValue(optimalCondition.Property.Name, out Property? property)) {
                    p = property;
                } else {
                    throw new Exception($"Property {optimalCondition.Property} was not found in the system.");
                }

                var valueHandler = _factory.GetValueHandlerImplementation(optimalCondition.Property.OwlType);
                var unsatisfiedConstraints = valueHandler.GetUnsatisfiedConstraintsFromEvaluation(optimalCondition.ConditionConstraint, p.Value);
                numberOfSatisfiedOptimalConditions += unsatisfiedConstraints.Any() ? 0 : 1;
            }

            return numberOfSatisfiedOptimalConditions;
        }

        private IEnumerable<SimulationPath> GetSimulationPathsWithMostOptimizedProperties(IEnumerable<SimulationPath> simulationPaths) {
            // Use any property cache to find optimization annotations. This assumes we're working with numerical types and not booleans.
            // TODO: include ConfigurableParameters.
            var propertiesToMinimize = GetPropertiesToOptimize(simulationPaths.First().Simulations.First().PropertyCache, false);
            var propertiesToMaximize = GetPropertiesToOptimize(simulationPaths.First().Simulations.First().PropertyCache, true);

            var mostMinimizedSimulationPathsMap = new Dictionary<string, List<SimulationPath>>();
            var mostMaximizedSimulationPathsMap = new Dictionary<string, List<SimulationPath>>();

            // Find simulation paths with the most minimized properties. Save the path if it's the best one for at least one property.
            foreach (var minimizedProperty in propertiesToMinimize) {
                var lowestTotalPropertyValue = double.MaxValue;

                foreach (var simulationPath in simulationPaths) {
                    var totalMinimizedPropertyValue = 0.0;

                    foreach (var simulation in simulationPath.Simulations) {
                        totalMinimizedPropertyValue += (double)simulation.PropertyCache.Properties[minimizedProperty.Name].Value;
                    }

                    if (totalMinimizedPropertyValue < lowestTotalPropertyValue) {
                        lowestTotalPropertyValue = totalMinimizedPropertyValue;

                        mostMinimizedSimulationPathsMap[minimizedProperty.Name] = [simulationPath];
                    } else if (totalMinimizedPropertyValue == lowestTotalPropertyValue) {
                        mostMinimizedSimulationPathsMap[minimizedProperty.Name].Add(simulationPath);
                    }
                }
            }

            // Find simulation paths with the most maximized properties. Save the path if it's the best one for at least one property.
            foreach (var maximizedProperty in propertiesToMaximize) {
                var highestTotalPropertyValue = double.MinValue;

                foreach (var simulationPath in simulationPaths) {
                    var totalMaximizedPropertyValue = 0.0;

                    foreach (var simulation in simulationPath.Simulations) {
                        totalMaximizedPropertyValue += (double)simulation.PropertyCache.Properties[maximizedProperty.Name].Value;
                    }

                    if (totalMaximizedPropertyValue > highestTotalPropertyValue) {
                        highestTotalPropertyValue = totalMaximizedPropertyValue;

                        mostMaximizedSimulationPathsMap[maximizedProperty.Name] = [simulationPath];
                    } else if (totalMaximizedPropertyValue == highestTotalPropertyValue) {
                        mostMaximizedSimulationPathsMap[maximizedProperty.Name].Add(simulationPath);
                    }
                }
            }

            
            // Find how many properties a simulation path optimized best.
            var mostOptimizedSimulationPathsCounter = new Dictionary<SimulationPath, int>();

            // XXX Hotfix, review:
            if (mostMinimizedSimulationPathsMap.Count == 0) {
                mostMinimizedSimulationPathsMap.Add("", simulationPaths.ToList());
            }
            foreach (var value in mostMinimizedSimulationPathsMap.Values) {
                foreach (var simulationPath in value) {
                    // Increment the counter on a simulation path that minimized a property the most.
                    if (mostOptimizedSimulationPathsCounter.ContainsKey(simulationPath)) {
                        mostOptimizedSimulationPathsCounter[simulationPath]++;
                    } else {
                        mostOptimizedSimulationPathsCounter.Add(simulationPath, 1);
                    }
                }
            }

            foreach (var keyValuePair in mostMaximizedSimulationPathsMap) {
                foreach (var simulationPath in keyValuePair.Value) {
                    // Increment the counter on a simulation path that maximized a property the most.
                    if (mostOptimizedSimulationPathsCounter.ContainsKey(simulationPath)) {
                        mostOptimizedSimulationPathsCounter[simulationPath]++;
                    } else {
                        mostOptimizedSimulationPathsCounter.Add(simulationPath, 1);
                    }
                }
            }

            // Return the one(s) that optimized the most properties.
            var mostOptimizedSimulationPaths = new List<SimulationPath>();
            var mostPropertiesOptimized = 0;

            foreach (var keyValuePair in mostOptimizedSimulationPathsCounter) {
                if (keyValuePair.Value > mostPropertiesOptimized) {
                    mostPropertiesOptimized = keyValuePair.Value;

                    mostOptimizedSimulationPaths = [keyValuePair.Key];
                } else if (keyValuePair.Value == mostPropertiesOptimized) {
                    mostOptimizedSimulationPaths.Add(keyValuePair.Key);
                }
            }

            return mostOptimizedSimulationPaths ;
        }

        private List<Property> GetPropertiesToOptimize(PropertyCache propertyCache, bool maximize) {
            var propertyList = new List<Property>();

            // Simple injection. We can achieve this similarly with dotNetRdf's own methods with full URIs.
            var filter = maximize ? "meta:maximizes" : "meta:minimizes";

            var query = _mapekKnowledge.GetParameterizedStringQuery(@"SELECT ?propertyName WHERE {
                ?platform rdf:type sosa:Platform .
                ?platform " + filter + " ?propertyName . }");

            var queryResult = _mapekKnowledge.ExecuteQuery(query);

            foreach (var result in queryResult.Results) {
                var propertyName = result["propertyName"].ToString();
                if (propertyCache.Properties.TryGetValue(propertyName, out var property)) {
                    // Of the 3 supported types, we don't want to handle non-numericals for this.
                    if (property.OwlType != "http://www.w3.org/2001/XMLSchema#boolean") {
                        propertyList.Add(property);
                    }
                } else {
                    throw new Exception($"Property {propertyName} not found in cache.");
                }
            }

            return propertyList;
        }

        internal void LogOptimalSimulationPath(SimulationPath optimalSimulationPath)
        {
            if (optimalSimulationPath is null) {
                return;
            }

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

        public void Dispose() {
            foreach (var d in _iDict) {
                _logger.LogDebug("Disposing instance of {fmu}.", d.Key);
                d.Value.Dispose();
            }
            foreach (var d in _fmuDict) {
                _logger.LogDebug("Disposing {fmu}.", d.Key);
                d.Value.Dispose();
            }
        }
    }
}
