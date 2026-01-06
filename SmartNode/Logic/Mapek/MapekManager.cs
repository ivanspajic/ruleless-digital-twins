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
        private const int SleepyTimeMilliseconds = 2_000;

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

        public void StartLoop() {
            _isLoopActive = true;
            RunMapekLoop();
        }

        public void StopLoop() {
            _isLoopActive = false;
        }

        private void RunMapekLoop() {
            _logger.LogInformation("Starting the MAPE-K loop. (maxRounds= {maxRound})", _coordinatorSettings.MaximumMapekRounds);

            var currentRound = 0;
            Case potentialCase = null!;
            List<Case> potentialCases = null!;

            while (_isLoopActive) {
                if (_coordinatorSettings.MaximumMapekRounds > -1) {
                    _logger.LogInformation("MAPE-K rounds left: {maxRound})", _coordinatorSettings.MaximumMapekRounds);
                }

                // Reload the instance model for each cycle to ensure dynamic model updates are captured.
                _mapekKnowledge.LoadModelsFromKnowledgeBase(); // This makes sense in theory but won't work without the Factory updating as well.

                // Monitor - Observe all hard and soft Sensor values, construct soft Sensor trees, and collect OptimalConditions.
                var cache = _mapekMonitor.Monitor();

                // If there is a potential case from the previous cycle to be saved, check if all the required parameters match. Most importantly, check that the quantized values of
                // the observed Properties from this cycle match with the predicted quantized values from the simulation of the last cycle. If so, the previously-created case is valid
                // and can be saved to the database.
                var quantizedPropertyCacheOptimalConditionTuple = GetQuantizedPropertiesAndOptimalConditions(cache.PropertyCache.ConfigurableParameters,
                        cache.PropertyCache.Properties,
                        cache.OptimalConditions);

                if (potentialCase is not null) {
                    var caseMatches = CheckIfCaseResultMatchesObservedParameters(potentialCase,
                        quantizedPropertyCacheOptimalConditionTuple.Item1,
                        quantizedPropertyCacheOptimalConditionTuple.Item2);

                    // If the case matches with this cycle's observed parameters, save it. If the case cannot be saved, the remainder of any sequence of cases (SimulationPath) it
                    // belongs to must also be discarded.
                    if (caseMatches) {
                        _caseRepository.CreateCase(potentialCase);
                    } else {
                        potentialCases = [];
                    }

                    // If there is a sequence of cases to execute, get the next potential case from it.
                    if (potentialCases.Count > 0) {
                        potentialCase = GetPotentialCaseAndRemoveItFromCollection(potentialCases);
                    } else {
                        potentialCase = null!;
                    }
                }

                // If there is no potential case from a sequence of cases, try finding a match for the current conditions in the database. In case of no match, run the Plan phase and
                // simulate future cycles.
                if (potentialCase is null) {
                    potentialCase = _caseRepository.GetCase(quantizedPropertyCacheOptimalConditionTuple.Item1,
                        quantizedPropertyCacheOptimalConditionTuple.Item2,
                        _coordinatorSettings.LookAheadMapekCycles,
                        _coordinatorSettings.SimulationDurationSeconds,
                        0);

                    if (string.IsNullOrEmpty(potentialCase?.ID)) {
                        // Plan - Simulate all Actions and check that they mitigate OptimalConditions and optimize the system to get the most optimal configuration.
                        // TODO: use the simulation tree for visualization.
                        var (simulationTree, optimalSimulationPath) = _mapekPlan.Plan(cache, _coordinatorSettings.LookAheadMapekCycles);

                        // Build and assign the potential cases for the next cycle from the simulation results.
                        potentialCases = GetPotentialCasesFromSimulationPath(quantizedPropertyCacheOptimalConditionTuple.Item1, quantizedPropertyCacheOptimalConditionTuple.Item2, optimalSimulationPath);
                        potentialCase = GetPotentialCaseAndRemoveItFromCollection(potentialCases);
                    }
                }

                // Execute - Execute the Actuators with the appropriate ActuatorStates and/or adjust the values of ReconfigurableParameters.
                _mapekExecute.Execute(potentialCase.Simulation!, _coordinatorSettings.UseSimulatedEnvironment);

                // If configured, write MAPE-K state to CSV.
                if (_coordinatorSettings.SaveMapekCycleData) {
                    CsvUtils.WritePropertyStatesToCsv(_filepathArguments.DataDirectory, currentRound, cache.PropertyCache.ConfigurableParameters, cache.PropertyCache.Properties);
                    CsvUtils.WriteActuatorStatesToCsv(_filepathArguments.DataDirectory, currentRound, potentialCase.Simulation!);
                }

                if (_coordinatorSettings.MaximumMapekRounds > 0) {
                    _coordinatorSettings.MaximumMapekRounds--;
                }
                if (_coordinatorSettings.MaximumMapekRounds == 0) {
                    _isLoopActive = false;
                    break; // We can sleep when we're dead.
                }

                currentRound++;

                // Thread.Sleep(SleepyTimeMilliseconds);
            }
        }

        private static bool CheckIfCaseResultMatchesObservedParameters(Case potentialCaseToSave,
            IEnumerable<Property> quantizedObservedProperties,
            IEnumerable<OptimalCondition> observedQuantizedOptimalConditions) {
            return potentialCaseToSave.QuantizedProperties!.SequenceEqual(quantizedObservedProperties, new PropertyEqualityComparer()) &&
                potentialCaseToSave.QuantizedOptimalConditions!.SequenceEqual(observedQuantizedOptimalConditions, new OptimalConditionEqualityComparer());
        }

        private (IEnumerable<Property>, IEnumerable<OptimalCondition>) GetQuantizedPropertiesAndOptimalConditions(IDictionary<string, ConfigurableParameter> configurableParameters,
            IDictionary<string, Property> properties,
            IEnumerable<OptimalCondition> optimalConditions) {
            var quantizedProperties = new List<Property>();
            var quantizedOptimalConditions = new List<OptimalCondition>();

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

            foreach (var optimalCondition in optimalConditions) {
                var valueHandler = _factory.GetValueHandlerImplementation(optimalCondition.ConstraintValueType);
                var quantizedConstraints = new List<ConstraintExpression>();
                foreach (var constraint in optimalCondition.Constraints) {
                    var quantizedConstraint = GetQuantizedOptimalConditionConstraint(constraint, valueHandler);
                    quantizedConstraints.Add(quantizedConstraint);
                }
                quantizedOptimalConditions.Add(new OptimalCondition {
                    Constraints = quantizedConstraints,
                    ConstraintValueType = optimalCondition.ConstraintValueType,
                    Name = optimalCondition.Name,
                    Property = optimalCondition.Property,
                    ReachedInMaximumSeconds = optimalCondition.ReachedInMaximumSeconds,
                    UnsatisfiedAtomicConstraints = []
                });
            }

            return new (quantizedProperties, quantizedOptimalConditions);
        }

        private ConstraintExpression GetQuantizedOptimalConditionConstraint(ConstraintExpression constraintExpression, IValueHandler valueHandler) {
            // Go through the whole tree of OptimalCondition constraints and get quantized values for each one.
            if (constraintExpression is AtomicConstraintExpression atomicConstraintExpression) {
                return new AtomicConstraintExpression {
                    ConstraintType = atomicConstraintExpression.ConstraintType,
                    Right = valueHandler.GetQuantizedValue(atomicConstraintExpression.Right, _coordinatorSettings.PropertyValueFuzziness)
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

        private List<Case> GetPotentialCasesFromSimulationPath(IEnumerable<Property> quantizedObservedProperties,
            IEnumerable<OptimalCondition> quantizedObservedOptimalConditions,
            SimulationPath simulationPath) {
            var potentialCases = new List<Case>();
            var caseIndex = 0;

            foreach (var simulation in simulationPath.Simulations) {
                potentialCases.Add(new Case {
                    ID = null,
                    Index = caseIndex,
                    LookAheadCycles = _coordinatorSettings.LookAheadMapekCycles,
                    SimulationDurationSeconds = _coordinatorSettings.SimulationDurationSeconds,
                    QuantizedOptimalConditions = quantizedObservedOptimalConditions,
                    QuantizedProperties = quantizedObservedProperties,
                    Simulation = simulation
                });

                caseIndex++;
            }

            return potentialCases;
        }

        private static Case GetPotentialCaseAndRemoveItFromCollection(List<Case> potentialCases) {
            var potentialCase = potentialCases[0];
            potentialCases.Remove(potentialCase);

            return potentialCase;
        }
    }
}