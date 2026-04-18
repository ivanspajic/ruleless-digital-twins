using Logic.CaseRepository;
using Logic.Mapek.Comparers;
using Logic.Models.DatabaseModels;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Logic.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Diagnostics;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TestProject")]

namespace Logic.Mapek {
    public class MapekManager : IMapekManager {
        private const string SimulationTreeFilename = "simulation-tree.json";

        private readonly FilepathArguments _filepathArguments;
        private readonly CoordinatorSettings _coordinatorSettings;
        private readonly ILogger<IMapekManager> _logger;
        private readonly IMapekMonitor _mapekMonitor;
        private readonly IMapekPlan _mapekPlan;
        private readonly IMapekExecute _mapekExecute;
        private readonly IMapekKnowledge _mapekKnowledge;
        private readonly ICaseRepository _caseRepository;
        private readonly IBangBangPlanner _bangBangPlanner;

        private bool _isLoopActive = false;
        private bool _bufferedDecisionUsed = false;
        private bool _caseHit = false;

        public MapekManager(IServiceProvider serviceProvider) {
            _filepathArguments = serviceProvider.GetRequiredService<FilepathArguments>();
            _coordinatorSettings = serviceProvider.GetRequiredService<CoordinatorSettings>();
            _logger = serviceProvider.GetRequiredService<ILogger<IMapekManager>>();
            _mapekMonitor = serviceProvider.GetRequiredService<IMapekMonitor>();
            _mapekPlan = serviceProvider.GetRequiredService<IMapekPlan>();
            _mapekExecute = serviceProvider.GetRequiredService<IMapekExecute>();
            _mapekKnowledge = serviceProvider.GetRequiredService<IMapekKnowledge>();
            _caseRepository = serviceProvider.GetRequiredService<ICaseRepository>();
            _bangBangPlanner = serviceProvider.GetRequiredService<IBangBangPlanner>();
        }

        public async Task StartLoop() {
            _isLoopActive = true;
            await RunMapekLoop();
        }

        public void StopLoop() {
            _isLoopActive = false;
        }

        private async Task RunMapekLoop() {
            _logger.LogInformation("Starting the MAPE-K loop. (maxRounds= {maxRound})", _coordinatorSettings.MaximumMapekRounds);

            var currentMapekCycle = 0;
            Simulation simulationToExecute = null!;
            Case potentialCase = null!;
            SimulationTreeNode currentSimulationTree = null!;
            SimulationPath currentOptimalSimulationPath = null!;

            var stopwatch = new Stopwatch();

            while (_isLoopActive) {
                // Gather duration information for the dummy environment.
                stopwatch.Start();

                if (_coordinatorSettings.MaximumMapekRounds > -1) {
                    _logger.LogInformation("MAPE-K rounds left: {maxRound})", _coordinatorSettings.MaximumMapekRounds);
                }

                // Reload the instance model for each cycle to ensure dynamic model updates are captured.
                _mapekKnowledge.LoadModelsFromKnowledgeBase(); // This makes sense in theory but won't work without the Factory updating as well.

                // Monitor - Observe all hard and soft Sensor values, construct soft Sensor trees, and collect OptimalConditions.
                var cache = await _mapekMonitor.Monitor(currentMapekCycle);

                // Use the right planning method (and functionality) based on configuration.
                // TODO: Consider using the strategy pattern here. How do we make the Manager agnostic to the type of planner it should use? We should probably not have multi-cycle
                // simulation logic be outside the planner. This would allow us to simply return the simulation as required. The planner should probably decide on its own whether it
                // has something buffered based on the conditions observed.
                if (_coordinatorSettings.UseRulelessMethod) {
                    // Check for previously constructed simulation paths to pick the next simulation configuration to execute. If case-based functionality is enabled, check for preexisting
                    // cases and save new ones when applicable. For simplicity, the look-ahead approach and the case-based functionality effectively keep state based on the configuration at
                    // the time of making a sequence of simulations/cases to execute. This means if settings values are changed midway through a full simulation path execution (e.g., 2/4),
                    // the system will continue executing as if it followed the old ones for the remainder of the simulation path. Dynamic settings changes thus take effect after the execution
                    // of a full simulation path or in case it is deliberately rejected early by the system due to deviation from its previously predicted Property values.
                    (simulationToExecute, potentialCase, currentSimulationTree, currentOptimalSimulationPath) = await ManageSimulationsAndCasesAndPotentiallyPlan(cache,
                        simulationToExecute,
                        potentialCase,
                        currentSimulationTree,
                        currentOptimalSimulationPath,
                        currentMapekCycle);
                } else {
                    // Keeping it simple with a classic bang-bang controller.
                    simulationToExecute = _bangBangPlanner.Plan(cache);
                }

                stopwatch.Stop();

                // Execute - Execute the Actuators with the appropriate ActuatorStates and/or adjust the values of ReconfigurableParameters.
                await _mapekExecute.Execute(simulationToExecute, stopwatch.ElapsedMilliseconds / 1000.0);
                
                stopwatch.Reset();

                // If configured, write MAPE-K state to CSV.
                if (_coordinatorSettings.SaveMapekCycleData && simulationToExecute is not null) {
                    CsvUtils.WritePropertyStatesToCsv(_filepathArguments.DataDirectory, currentMapekCycle, cache.PropertyCache.ConfigurableParameters, cache.PropertyCache.Properties);
                    CsvUtils.WriteActuatorStatesToCsv(_filepathArguments.DataDirectory, currentMapekCycle, simulationToExecute);
                    CsvUtils.WritePropertyState(Path.Combine(_filepathArguments.DataDirectory, "bufferedDecisionsUsed.csv"), currentMapekCycle, "Buffered_Decision_Used", _bufferedDecisionUsed);
                    CsvUtils.WritePropertyState(Path.Combine(_filepathArguments.DataDirectory, "caseHits.csv"), currentMapekCycle, "Matching_Case_Found", _caseHit);

                    //var serializedSimulationTree = JsonConvert.SerializeObject(currentSimulationTree.SerializableSimulationTreeNode);
                    //File.WriteAllText(Path.Combine(_filepathArguments.DataDirectory, SimulationTreeFilename), serializedSimulationTree);
                }

                if (_coordinatorSettings.MaximumMapekRounds > 0) {
                    _coordinatorSettings.MaximumMapekRounds--;
                }
                if (_coordinatorSettings.MaximumMapekRounds == 0) {
                    _isLoopActive = false;
                    break; // We can sleep when we're dead.
                }

                currentMapekCycle++;

                _logger.LogInformation("Sleeping {sleepTime} ms until next MAPE-K cycle.", _coordinatorSettings.SleepyTimeMilliseconds);
                Thread.Sleep(_coordinatorSettings.SleepyTimeMilliseconds);
            }
        }

        private async Task<(Simulation, Case, SimulationTreeNode, SimulationPath)> ManageSimulationsAndCasesAndPotentiallyPlan(Cache cache,
            Simulation simulationToExecute,
            Case potentialCase,
            SimulationTreeNode currentSimulationTree,
            SimulationPath currentOptimalSimulationPath,
            int currentMapekCycle) {
            // Reset for tracking.
            _bufferedDecisionUsed = false;
            _caseHit = false;

            var observedProperties = new List<Property>(cache.PropertyCache.Properties.Values);
            observedProperties.AddRange(cache.PropertyCache.ConfigurableParameters.Values);

            var simulationMatches = false;
            if (simulationToExecute is not null) {
                // Use a set for comparing without caring for order.
                var simulationPropertiesSet = new HashSet<Property>(simulationToExecute.PropertyCache.Properties.Values, new FuzzyPropertyEqualityComparer(_coordinatorSettings.PropertyValueFuzziness));
                simulationMatches = simulationPropertiesSet.SetEquals(cache.PropertyCache.Properties.Values);
            }

            // If the previously executed simulation's results don't match the current cycle's observations, the predictions for the rest of the simulation path are outside of
            // previously predicted conditions and should thus be discarded.
            if (!simulationMatches) {
                currentOptimalSimulationPath = null!;
            }

            if (_coordinatorSettings.UseCaseBasedFunctionality && potentialCase is not null) {
                // If there is a potential case from the previous cycle to be saved and the values of the observed Properties from this cycle match with the predicted values
                // from the simulation of the last cycle, then case is valid and can be saved to the database. Otherwise, the case should be nullified.
                if (simulationMatches) {
                    _caseRepository.CreateCase(potentialCase);
                }

                // Reset in case of leftover potential cases.
                potentialCase = null!;

                // If there are still remaining simulations in the simulation path, get the next potential case from it. Otherwise, try to look for it in the database.
                if (currentOptimalSimulationPath is not null && currentOptimalSimulationPath.Simulations.Any()) {
                    
                    potentialCase = GetPotentialCaseFromSimulationPath(observedProperties, cache.OptimalConditions, currentOptimalSimulationPath);

                    // After getting the potential case from the simulation path, reduce the number of remaining simulations.
                    currentOptimalSimulationPath.RemoveFirstRemainingSimulationFromSimulationPath();
                } else {
                    currentOptimalSimulationPath = GetSimulationPathFromSavedCases(observedProperties, cache.OptimalConditions);
                    if (currentOptimalSimulationPath is not null) {
                        potentialCase = GetPotentialCaseFromSimulationPath(observedProperties, cache.OptimalConditions, currentOptimalSimulationPath);

                        // After getting the potential case from the simulation path, reduce the number of remaining simulations.
                        currentOptimalSimulationPath.RemoveFirstRemainingSimulationFromSimulationPath();

                        // For tracking case hits.
                        _caseHit = true;
                    }
                }
            }

            // For tracking buffered decision use.
            var planned = false;

            if (potentialCase is null) {
                // If there are no remaining simulations to be executed from a previously created simulation path, run the planning phase again.
                if (currentOptimalSimulationPath is null || !currentOptimalSimulationPath.Simulations.Any()) {
                    // Plan - Simulate all Actions and check that they mitigate OptimalConditions and optimize the system to get the most optimal configuration.
                    // TODO: use the simulation tree for visualization.
                    (currentSimulationTree, currentOptimalSimulationPath) = await _mapekPlan.Plan(cache, currentMapekCycle);

                    planned = true;
                }

                // If case-based functionality is used, get the potential case from the new simulation path.
                if (_coordinatorSettings.UseCaseBasedFunctionality) {
                    potentialCase = GetPotentialCaseFromSimulationPath(observedProperties, cache.OptimalConditions, currentOptimalSimulationPath);
                }

                if (currentOptimalSimulationPath != null) {
                    simulationToExecute = currentOptimalSimulationPath.Simulations.First();

                    // After getting the simulation from the simulation path, reduce the number of remaining simulations.
                    currentOptimalSimulationPath.RemoveFirstRemainingSimulationFromSimulationPath();

                    // For tracking buffered decision use.
                    _bufferedDecisionUsed = !planned;
                } else {
                    simulationToExecute = null!;
                }
            }

            return (simulationToExecute, potentialCase, currentSimulationTree, currentOptimalSimulationPath)!;
        }

        private SimulationPath GetSimulationPathFromSavedCases(IEnumerable<Property> quantizedProperties, IEnumerable<OptimalCondition> quantizedOptimalConditions) {
            var simulations = new List<Simulation>();

            for (var i = 0; i < _coordinatorSettings.LookAheadMapekCycles; i++) {
                var savedCase = _caseRepository.ReadCase(quantizedProperties,
                    quantizedOptimalConditions,
                    _coordinatorSettings.LookAheadMapekCycles,
                    _coordinatorSettings.CycleDurationSeconds,
                    i,
                    _coordinatorSettings.PropertyValueFuzziness);

                // If no case is found, return a null.
                if (savedCase is null) {
                    return null!;
                }

                simulations.Add(savedCase.Simulation);

                quantizedProperties = savedCase.Properties;
                quantizedOptimalConditions = savedCase.OptimalConditions;
            }

            return new SimulationPath {
                Simulations = simulations
            };
        }

        private Case GetPotentialCaseFromSimulationPath(IEnumerable<Property> quantizedObservedProperties,
            IEnumerable<OptimalCondition> quantizedObservedOptimalConditions,
            SimulationPath simulationPath) {
            var firstSimulation = simulationPath.Simulations.First();

            return new Case {
                ID = firstSimulation.GetHashCode().ToString(),
                Index = firstSimulation.Index,
                LookAheadCycles = _coordinatorSettings.LookAheadMapekCycles,
                CycleDurationSeconds = _coordinatorSettings.CycleDurationSeconds,
                OptimalConditions = quantizedObservedOptimalConditions,
                Properties = quantizedObservedProperties,
                Simulation = firstSimulation
            };
        }
    }
}