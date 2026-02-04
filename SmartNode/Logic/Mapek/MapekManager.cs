using Logic.CaseRepository;
using Logic.FactoryInterface;
using Logic.Mapek.Comparers;
using Logic.Models.DatabaseModels;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Logic.Utilities;
using Logic.ValueHandlerInterfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("TestProject")]

namespace Logic.Mapek {
    public class MapekManager : IMapekManager {

        private readonly FilepathArguments _filepathArguments;
        private readonly CoordinatorSettings _coordinatorSettings;
        private readonly ILogger<IMapekManager> _logger;
        private readonly IMapekMonitor _mapekMonitor;
        private readonly IMapekPlan _mapekPlan;
        private readonly IMapekExecute _mapekExecute;
        private readonly IMapekKnowledge _mapekKnowledge;
        private readonly ICaseRepository _caseRepository;
        private readonly IFactory _factory;

        private bool _isLoopActive = false;

        public MapekManager(IServiceProvider serviceProvider) {
            _filepathArguments = serviceProvider.GetRequiredService<FilepathArguments>();
            _coordinatorSettings = serviceProvider.GetRequiredService<CoordinatorSettings>();
            _logger = serviceProvider.GetRequiredService<ILogger<IMapekManager>>();
            _mapekMonitor = serviceProvider.GetRequiredService<IMapekMonitor>();
            _mapekPlan = serviceProvider.GetRequiredService<IMapekPlan>();
            _mapekExecute = serviceProvider.GetRequiredService<IMapekExecute>();
            _mapekKnowledge = serviceProvider.GetRequiredService<IMapekKnowledge>();
            _caseRepository = serviceProvider.GetRequiredService<ICaseRepository>();
            _factory = serviceProvider.GetRequiredService<IFactory>();
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

            var currentRound = 0;
            Simulation simulationToExecute = null!;
            Case potentialCase = null!;
            SimulationTreeNode currentSimulationTree = null!;
            SimulationPath currentOptimalSimulationPath = null!;

            while (_isLoopActive) {
                if (_coordinatorSettings.MaximumMapekRounds > -1) {
                    _logger.LogInformation("MAPE-K rounds left: {maxRound})", _coordinatorSettings.MaximumMapekRounds);
                }

                // Reload the instance model for each cycle to ensure dynamic model updates are captured.
                _mapekKnowledge.LoadModelsFromKnowledgeBase(); // This makes sense in theory but won't work without the Factory updating as well.

                // Monitor - Observe all hard and soft Sensor values, construct soft Sensor trees, and collect OptimalConditions.
                var cache = await _mapekMonitor.Monitor();

                // Check for previously constructed simulation paths to pick the next simulation configuration to execute. If case-based functionality is enabled, check for preexisting
                // cases and save new ones when applicable. For simplicity, the look-ahead approach and the case-based functionality effectively keep state based on the configuration at
                // the time of making a sequence of simulations/cases to execute. This means if settings values are changed midway through a full simulation path execution (e.g., 2/4),
                // the system will continue executing as if it followed the old ones for the remainder of the simulation path. Dynamic settings changes thus take effect after the execution
                // of a full simulation path or in case it is deliberately rejected early by the system due to deviation from its previously predicted Property values.
                (simulationToExecute, potentialCase, currentSimulationTree, currentOptimalSimulationPath) = await ManageSimulationsAndCasesAndPotentiallyPlan(cache,
                    simulationToExecute,
                    potentialCase,
                    currentSimulationTree,
                    currentOptimalSimulationPath);

                // Execute - Execute the Actuators with the appropriate ActuatorStates and/or adjust the values of ReconfigurableParameters.
                await _mapekExecute.Execute(simulationToExecute);

                // If configured, write MAPE-K state to CSV.
                if (_coordinatorSettings.SaveMapekCycleData) {
                    CsvUtils.WritePropertyStatesToCsv(_filepathArguments.DataDirectory, currentRound, cache.PropertyCache.ConfigurableParameters, cache.PropertyCache.Properties);
                    CsvUtils.WriteActuatorStatesToCsv(_filepathArguments.DataDirectory, currentRound, potentialCase.Simulation);
                }

                if (_coordinatorSettings.MaximumMapekRounds > 0) {
                    _coordinatorSettings.MaximumMapekRounds--;
                }
                if (_coordinatorSettings.MaximumMapekRounds == 0) {
                    _isLoopActive = false;
                    break; // We can sleep when we're dead.
                }

                currentRound++;

                _logger.LogInformation("Sleeping {sleepTime} ms until next MAPE-K cycle.", _coordinatorSettings.SleepyTimeMilliseconds);
                Thread.Sleep(_coordinatorSettings.SleepyTimeMilliseconds);
            }
        }

        private async Task<(Simulation, Case, SimulationTreeNode, SimulationPath)> ManageSimulationsAndCasesAndPotentiallyPlan(Cache cache,
            Simulation simulationToExecute,
            Case potentialCase,
            SimulationTreeNode currentSimulationTree,
            SimulationPath currentOptimalSimulationPath) {
            var quantizedObservedProperties = GetQuantizedProperties(cache.PropertyCache.ConfigurableParameters, cache.PropertyCache.Properties);

            var simulationMatches = false;
            if (simulationToExecute is not null) {
                var quantizedSimulationProperties = GetQuantizedProperties(simulationToExecute.PropertyCache.ConfigurableParameters, simulationToExecute.PropertyCache.Properties);

                simulationMatches = quantizedSimulationProperties.SequenceEqual(quantizedObservedProperties, new PropertyEqualityComparer());
            }

            // If the previously executed simulation's results don't match the current cycle's observations, the predictions for the rest of the simulation path are outside of
            // previously predicted conditions and should thus be discarded.
            if (!simulationMatches) {
                currentOptimalSimulationPath = null!;
            }

            if (_coordinatorSettings.UseCaseBasedFunctionality && potentialCase is not null) {
                // If there is a potential case from the previous cycle to be saved and the values of the observed Properties from this cycle match with the predicted quantized
                // values from the simulation of the last cycle, then case is valid and can be saved to the database. Otherwise, the case should be nullified.
                if (simulationMatches) {
                    _caseRepository.CreateCase(potentialCase);
                } else {
                    potentialCase = null!;
                }

                var quantizedObservedOptimalConditions = GetQuantizedOptimalConditions(cache.OptimalConditions);

                // If there are still remaining simulations in the simulation path, get the next potential case from it. Otherwise, try to look for it in the database.
                if (currentOptimalSimulationPath is not null && currentOptimalSimulationPath.Simulations.Any()) {
                    
                    potentialCase = GetPotentialCaseFromSimulationPath(quantizedObservedProperties, quantizedObservedOptimalConditions, currentOptimalSimulationPath);

                    // After getting the potential case from the simulation path, reduce the number of remaining simulations.
                    currentOptimalSimulationPath.RemoveFirstRemainingSimulationFromSimulationPath();
                } else {
                    currentOptimalSimulationPath = GetSimulationPathFromSavedCases(quantizedObservedProperties, quantizedObservedOptimalConditions);
                    if (currentOptimalSimulationPath is not null) {
                        potentialCase = GetPotentialCaseFromSimulationPath(quantizedObservedProperties, quantizedObservedOptimalConditions, currentOptimalSimulationPath);

                        // After getting the potential case from the simulation path, reduce the number of remaining simulations.
                        currentOptimalSimulationPath.RemoveFirstRemainingSimulationFromSimulationPath();
                    }
                }
            }
            
            if (potentialCase is null) {
                // If there are no remaining simulations to be executed from a previously created simulation path, run the planning phase again.
                if (currentOptimalSimulationPath is null || !currentOptimalSimulationPath.Simulations.Any()) {
                    // Plan - Simulate all Actions and check that they mitigate OptimalConditions and optimize the system to get the most optimal configuration.
                    // TODO: use the simulation tree for visualization.
                    (currentSimulationTree, currentOptimalSimulationPath) = await _mapekPlan.Plan(cache);
                }

                // If case-based functionality is used, get the potential case from the new simulation path.
                if (_coordinatorSettings.UseCaseBasedFunctionality) {
                    var quantizedObservedOptimalConditions = GetQuantizedOptimalConditions(cache.OptimalConditions);

                    potentialCase = GetPotentialCaseFromSimulationPath(quantizedObservedProperties, quantizedObservedOptimalConditions, currentOptimalSimulationPath);
                }

                if (currentOptimalSimulationPath != null) {
                    simulationToExecute = currentOptimalSimulationPath.Simulations.First();

                    // After getting the simulation from the simulation path, reduce the number of remaining simulations.
                    currentOptimalSimulationPath.RemoveFirstRemainingSimulationFromSimulationPath();
                } else {
                    simulationToExecute = null!;
                }
            }

            return (simulationToExecute, potentialCase, currentSimulationTree, currentOptimalSimulationPath)!;
        }

        private List<Property> GetQuantizedProperties(IDictionary<string, ConfigurableParameter> configurableParameters,
            IDictionary<string, Property> properties) {
            var quantizedProperties = new List<Property>();

            foreach (var configurableParameterKeyValuePair in configurableParameters) {
                var valueHandler = _factory.GetValueHandlerImplementation(configurableParameterKeyValuePair.Value.OwlType);
                var quantizedValue = valueHandler.GetQuantizedValue(configurableParameterKeyValuePair.Value.Value, _coordinatorSettings.PropertyValueFuzziness);
                var quantizedConfigurableParameter = new ConfigurableParameter {
                    Name = configurableParameterKeyValuePair.Value.Name,
                    OwlType = configurableParameterKeyValuePair.Value.OwlType,
                    Value = quantizedValue
                };
                quantizedProperties.Add(quantizedConfigurableParameter);
            }

            foreach (var propertyKeyValuePair in properties) {
                var valueHandler = _factory.GetValueHandlerImplementation(propertyKeyValuePair.Value.OwlType);
                var quantizedValue = valueHandler.GetQuantizedValue(propertyKeyValuePair.Value.Value, _coordinatorSettings.PropertyValueFuzziness);
                var quantizedProperty = new Property {
                    Name = propertyKeyValuePair.Value.Name,
                    OwlType = propertyKeyValuePair.Value.OwlType,
                    Value = quantizedValue
                };
                quantizedProperties.Add(quantizedProperty);
            }

            return quantizedProperties;
        }

        private List<OptimalCondition> GetQuantizedOptimalConditions(IEnumerable<OptimalCondition> optimalConditions) {
            var quantizedOptimalConditions = new List<OptimalCondition>();

            foreach (var optimalCondition in optimalConditions) {
                var valueHandler = _factory.GetValueHandlerImplementation(optimalCondition.Property.OwlType);
                var constraint = GetQuantizedOptimalConditionConstraint(optimalCondition.Constraint, valueHandler);

                quantizedOptimalConditions.Add(new OptimalCondition {
                    Constraint = constraint,
                    Name = optimalCondition.Name,
                    Property = optimalCondition.Property,
                    ReachedInMaximumSeconds = optimalCondition.ReachedInMaximumSeconds
                });
            }

            return quantizedOptimalConditions;
        }

        private ConstraintExpression GetQuantizedOptimalConditionConstraint(ConstraintExpression constraintExpression, IValueHandler valueHandler) {
            // Go through the whole tree of OptimalCondition constraints and get quantized values for each one.
            if (constraintExpression is AtomicConstraintExpression atomicConstraintExpression) {
                var quantizedProperty = new Property {
                    Name = atomicConstraintExpression.Property.Name,
                    OwlType = atomicConstraintExpression.Property.OwlType,
                    Value = valueHandler.GetQuantizedValue(atomicConstraintExpression.Property.Value, _coordinatorSettings.PropertyValueFuzziness)
                };

                return new AtomicConstraintExpression {
                    ConstraintType = atomicConstraintExpression.ConstraintType,
                    Property = quantizedProperty
                };
            } else {
                var nestedConstraintExpression = constraintExpression as NestedConstraintExpression;

                return new NestedConstraintExpression {
                    ConstraintType = nestedConstraintExpression!.ConstraintType,
                    Left = GetQuantizedOptimalConditionConstraint(nestedConstraintExpression.Left, valueHandler),
                    Right = GetQuantizedOptimalConditionConstraint(nestedConstraintExpression.Right, valueHandler)
                };
            }
        }

        private SimulationPath GetSimulationPathFromSavedCases(IEnumerable<Property> quantizedProperties, IEnumerable<OptimalCondition> quantizedOptimalConditions) {
            var simulations = new List<Simulation>();

            for (var i = 0; i < _coordinatorSettings.LookAheadMapekCycles; i++) {
                var savedCase = _caseRepository.ReadCase(quantizedProperties,
                    quantizedOptimalConditions,
                    _coordinatorSettings.LookAheadMapekCycles,
                    _coordinatorSettings.SimulationDurationSeconds,
                    i);

                // If no case is found, return a null.
                if (savedCase is null) {
                    return null!;
                }

                simulations.Add(savedCase.Simulation);

                quantizedProperties = savedCase.QuantizedProperties;
                quantizedOptimalConditions = savedCase.QuantizedOptimalConditions;
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
                ID = null,
                Index = firstSimulation.Index,
                LookAheadCycles = _coordinatorSettings.LookAheadMapekCycles,
                SimulationDurationSeconds = _coordinatorSettings.SimulationDurationSeconds,
                QuantizedOptimalConditions = quantizedObservedOptimalConditions,
                QuantizedProperties = quantizedObservedProperties,
                Simulation = firstSimulation
            };
        }
    }
}