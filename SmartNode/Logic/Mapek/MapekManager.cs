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
        // Decides on the number of MAPE-K cycles to look ahead for and thus the number of simulation steps to run. For example,
        // setting this value to 1 means only simulating for the current cycle, while setting it to 4 means simulating for the next
        // 4 cycles.
        private readonly FilepathArguments _filepathArguments;
        private readonly CoordinatorSettings _coordinatorSettings;
        private readonly ILogger<IMapekManager> _logger;
        private readonly IMapekMonitor _mapekMonitor;
        //private readonly IMapekAnalyze _mapekAnalyze;
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
            //_mapekAnalyze = serviceProvider.GetRequiredService<IMapekAnalyze>();
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
            Case potentialCaseToSave = null!;

            while (_isLoopActive) {
                if (_coordinatorSettings.MaximumMapekRounds > -1) {
                    _logger.LogInformation("MAPE-K rounds left: {maxRound})", _coordinatorSettings.MaximumMapekRounds);
                }

                // Reload the instance model for each cycle to ensure dynamic model updates are captured.
                _mapekKnowledge.LoadModelsFromKnowledgeBase(); // This makes sense in theory but won't work without the Factory updating as well.

                // Monitor - Observe all hard and soft Sensor values, construct soft Sensor trees, and collect OptimalConditions.
                var cache = _mapekMonitor.Monitor();

                

                // ==================== THIS IS OBSOLETE =====================
                // Analyze - Out of all possible Actions, filter out the irrelevant ones based on current Property values and return
                // them with all OptimalConditions.
                // var optimalConditionsAndActions = _mapekAnalyze.Analyze(instanceModel, propertyCache, ConfigurableParameterGranularity);
                // ===========================================================



                // If there is a potential case from the previous cycle to be saved, check if all the required parameters match. Most importantly, check that the quantized values of
                // the observed Properties from this cycle match with the predicted quantized values from the simulation of the last cycle. If so, the previously-created case is valid
                // and can be saved to the database.
                if (potentialCaseToSave != null) {
                    var quantizedPropertyCacheOptimalConditionTuple = GetQuantizedPropertiesAndOptimalConditions(cache.PropertyCache.ConfigurableParameters,
                        cache.PropertyCache.Properties,
                        cache.OptimalConditions);
                    var caseMatches = CheckIfCaseMatchesObservedParameters(potentialCaseToSave,
                        quantizedPropertyCacheOptimalConditionTuple.Item1,
                        quantizedPropertyCacheOptimalConditionTuple.Item2,
                        quantizedPropertyCacheOptimalConditionTuple.Item3);

                    if (caseMatches) {
                        _caseRepository.CreateCase(potentialCaseToSave);
                    }
                }

                // check if the observed property values (quantized) with the optimal conditions (quantized) match any case in the database. if so, execute it and skip the plan phase
                // otherwise, execute the plan phase



                // Plan - Simulate all Actions and check that they mitigate OptimalConditions and optimize the system to get the most optimal configuration.
                var optimalSimulationPath = _mapekPlan.Plan(cache, _coordinatorSettings.LookAheadMapekCycles);



                // build a case from the 



                // Execute - Execute the Actuators with the appropriate ActuatorStates and/or adjust the values of ReconfigurableParameters.
                _mapekExecute.Execute(optimalSimulationPath., _coordinatorSettings.UseSimulatedEnvironment);

                // Write MAPE-K state to CSV.
                CsvUtils.WritePropertyStatesToCsv(_filepathArguments.DataDirectory, currentRound, cache.PropertyCache);
                CsvUtils.WriteActuatorStatesToCsv(_filepathArguments.DataDirectory, currentRound, optimalSimulationPath);

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

        private bool CheckIfCaseMatchesObservedParameters(Case potentialCaseToSave,
            IDictionary<string, ConfigurableParameter> configurableParameterKeyValues,
            IDictionary<string, Property> propertyKeyValues,
            IEnumerable<OptimalCondition> observedOptimalConditions) {
            return potentialCaseToSave.Simulation!.PropertyCache!.ConfigurableParameters.Values.SequenceEqual(configurableParameterKeyValues.Values, new PropertyComparer()) &&
                potentialCaseToSave.Simulation!.PropertyCache!.Properties.Values.SequenceEqual(propertyKeyValues.Values, new PropertyComparer()) &&
                potentialCaseToSave.QuantizedOptimalConditions!.SequenceEqual(observedOptimalConditions, new OptimalConditionComparer());
        }

        private Tuple<IDictionary<string, ConfigurableParameter>, IDictionary<string, Property>, IEnumerable<OptimalCondition>> GetQuantizedPropertiesAndOptimalConditions(IDictionary<string, ConfigurableParameter> configurableParameters,
            IDictionary<string, Property> properties,
            IEnumerable<OptimalCondition> optimalConditions) {
            var quantizedConfigurableParameters = new Dictionary<string, ConfigurableParameter>();
            var quantizedProperties = new Dictionary<string, Property>();
            var quantizedOptimalConditions = new List<OptimalCondition>();

            foreach (var configurableParameterKeyValuePair in configurableParameters) {
                var valueHandler = _factory.GetValueHandlerImplementation(configurableParameterKeyValuePair.Value.OwlType);
                var quantizedValue = valueHandler.GetQuantizedValue(configurableParameterKeyValuePair.Value.Value, _coordinatorSettings.PropertyValueFuzziness);
                var quantizedConfigurableParameter = new ConfigurableParameter {
                    Name = configurableParameterKeyValuePair.Value.Name,
                    OwlType = configurableParameterKeyValuePair.Value.OwlType,
                    Value = quantizedValue
                };
                quantizedConfigurableParameters.Add(configurableParameterKeyValuePair.Key, quantizedConfigurableParameter);
            }

            foreach (var propertyKeyValuePair in properties) {
                var valueHandler = _factory.GetValueHandlerImplementation(propertyKeyValuePair.Value.OwlType);
                var quantizedValue = valueHandler.GetQuantizedValue(propertyKeyValuePair.Value.Value, _coordinatorSettings.PropertyValueFuzziness);
                var quantizedProperty = new Property {
                    Name = propertyKeyValuePair.Value.Name,
                    OwlType = propertyKeyValuePair.Value.OwlType,
                    Value = quantizedValue
                };
                quantizedProperties.Add(propertyKeyValuePair.Key, quantizedProperty);
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
                    UnsatisfiedAtomicConstraints = new List<AtomicConstraintExpression>()
                });
            }

            return new Tuple<IDictionary<string, ConfigurableParameter>, IDictionary<string, Property>, IEnumerable<OptimalCondition>>(quantizedConfigurableParameters, quantizedProperties, quantizedOptimalConditions);
        }

        private ConstraintExpression GetQuantizedOptimalConditionConstraint(ConstraintExpression constraintExpression, IValueHandler valueHandler) {
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
    }
}