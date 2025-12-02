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
        // Required as fields to preserve caching throughout multiple MAPE-K loop cycles.
	    private static readonly Dictionary<string, IModel> _fmuDict = [];
	    private static readonly Dictionary<string, IInstance> _iDict = [];

        private readonly ILogger<MapekPlan> _logger;
        private readonly IFactory _factory;
        private readonly IMapekKnowledge _mapekKnowledge;

        private const int MaximumSimulationTimeSeconds = 900;

        public MapekPlan(IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetRequiredService<ILogger<MapekPlan>>();
            _factory = serviceProvider.GetRequiredService<IFactory>();
            _mapekKnowledge = serviceProvider.GetRequiredService<IMapekKnowledge>();
        }

        public SimulationConfiguration Plan(PropertyCache propertyCache, string fmuDirectory, int lookAheadCycles)
        {
            _logger.LogInformation("Starting the Plan phase.");

            _logger.LogInformation("Generating simulation configurations.");

            var optimalConditions = GetAllOptimalConditions(propertyCache);

            // Get all combinations of possible simulation configurations for the given number of cycles.
            var simulationConfigurations = GetSimulationConfigurations(lookAheadCycles);

            _logger.LogInformation("Generated a total of {total} simulation configurations.", simulationConfigurations.Count);

            // Execute the simulations and obtain their results.
            Simulate(simulationConfigurations, propertyCache, fmuDirectory);

            // Find the optimal simulation configuration.
            var optimalConfiguration = GetOptimalConfiguration(propertyCache, optimalConditions, simulationConfigurations);

            if (optimalConfiguration != null)
            {
                LogOptimalSimulationConfiguration(optimalConfiguration);
            }

            return optimalConfiguration!;
        }

        private List<SimulationConfiguration> GetSimulationConfigurations(int lookAheadCycles)
        {
            // TODO: figure out an iterator implementation with 'yield return'.
            return new List<SimulationConfiguration>();
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
 
        public static HashSet<HashSet<T>> OldGetNaryCartesianProducts<T>(IEnumerable<IEnumerable<T>> originalCollectionOfCollections)
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
                        var singleElementCombination = new HashSet<T>() { element };

                        combinations.Add(singleElementCombination);
                    }
                    else
                    {
                        // If there are remaining collections, get their n-ary Cartesian product and add the current element to all sets returned.
                        var remainingCombinations = OldGetNaryCartesianProducts(collectionOfRemainingCollections);

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

        private void Simulate(IEnumerable<SimulationConfiguration> simulationConfigurations, PropertyCache propertyCache, string fmuDirectory)
        {
            // Retrieve the host platform FMU and its simulation fidelity for ActuationAction simulations.
            var fmuModel = _mapekKnowledge.GetHostPlatformFmuModel(simulationConfigurations.First(), fmuDirectory);

            // Measure simulation time.
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            int i = 0;
            // TODO: Parallelize simulations (#13).
            foreach (var simulationConfiguration in simulationConfigurations)
            {
                _logger.LogInformation("Running simulation #{run}", i++);

                // Make a deep copy of the property cache for the current simulation configuration.
                var propertyCacheCopy = GetPropertyCacheCopy(propertyCache);

                if (simulationConfiguration.SimulationTicks.Any())
                {
                    // TODO: pass `fmuModel` instead of (some of) its components?
                    ExecuteActuationActionFmu(fmuModel.FilePath, simulationConfiguration, propertyCacheCopy, fmuModel.SimulationFidelitySeconds);
                }

                if (simulationConfiguration.PostTickActions.Any())
                {
                    // Executing/simulating soft sensors during the Plan phase is not yet supported.
                }

                // Assign the final Property values to the results of the simulation configuration.
                simulationConfiguration.ResultingPropertyCache = propertyCacheCopy;
            }

            stopwatch.Stop();
            _logger.LogInformation("Total simulation time (minutes): {elapsedTime}", (double)stopwatch.ElapsedMilliseconds / 1000 / 60);
        }

        private static PropertyCache GetPropertyCacheCopy(PropertyCache originalPropertyCache)
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

        private void ExecuteActuationActionFmu(string fmuFilePath, SimulationConfiguration simulationConfiguration, PropertyCache propertyCacheCopy, int simulationFidelitySeconds)
        {
            // The LogDebug calls here are primarily to keep an eye on crashes in the FMU which are otherwise a tad harder to track down.
            _logger.LogInformation("Simulation {simulationConfiguration} ({ticks} ticks)", simulationConfiguration, simulationConfiguration.SimulationTicks.Count());
            if (!_fmuDict.TryGetValue(fmuFilePath, out IModel? model))
            {
                _logger.LogDebug("Loading Model {filePath}", fmuFilePath);
                model = Model.Load(fmuFilePath, new Collection<UnsupportedFunctions>([UnsupportedFunctions.SetTime2]));
                _fmuDict.Add(fmuFilePath, model);
            }
            Debug.Assert(model != null, "Model is null after loading.");
            // We're only using one instance per FMU, so we can just use the path as name.
            var instanceName = fmuFilePath;
            if (!_iDict.TryGetValue(instanceName, out IInstance? fmuInstance))
            {
                _logger.LogDebug("Creating instance.");
                fmuInstance = model.CreateCoSimulationInstance(instanceName);
                _iDict.Add(instanceName, fmuInstance);

                _logger.LogDebug("Setting time");
                fmuInstance.StartTime(0);
            }
            else
            {
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
                var observableProperties = GetObservablePropertiesFromPropertyCache(propertyCacheCopy);

                foreach (var observableProperty in observableProperties)
                {
                    // Shave off the long name URIs from the instance model.
                    var simpleObservablePropertyName = MapekUtilities.GetSimpleName(observableProperty.Name);
                    fmuActuationInputs.Add((simpleObservablePropertyName, observableProperty.OwlType, observableProperty.Value));
                }

                // Add all ActuatorStates to the inputs for the FMU.
                foreach (var actuationAction in simulationTick.ActuationActions)
                {
                    // Shave off the long name URIs from the instance model.
                    var simpleActuatorName = MapekUtilities.GetSimpleName(actuationAction.Actuator.Name);
                    fmuActuationInputs.Add((simpleActuatorName + "State", "int", actuationAction.NewStateValue));
                }

                _logger.LogInformation("Parameters: {p}", string.Join(", ", fmuActuationInputs.Select(i => i.ToString())));
                AssignSimulationInputsToParameters(model, fmuInstance, fmuActuationInputs);

                _logger.LogDebug("Tick");
                // Advance the FMU time for the duration of the simulation tick in steps of simulation fidelity.
                var maximumSteps = (double)MaximumSimulationTimeSeconds / simulationFidelitySeconds;
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

                    if (!unsatisfiedConstraints.Any())
                    {
                        numberOfSatisfiedOptimalConditions++;
                    }
                }
            }

            return numberOfSatisfiedOptimalConditions;
        }

        private SimulationConfiguration GetOptimalConfiguration(PropertyCache propertyCache,
            IEnumerable<OptimalCondition> optimalConditions,
            IEnumerable<SimulationConfiguration> simulationConfigurations)
        {
            // This method is a filter for finding the optimal simulation configuration. It works in a few steps of descending precedance, each of which further reduces the set of
            // simulation configurations:
            // 1. Filter for simulation configurations that satisfy the most OptimalConditions.
            // 2. Filter for simulation configurations that have the highest number of the most optimized Properties.
            // 3. Pick the first one.

            if (!simulationConfigurations.Any())
            {
                return null!;
            }

            // Filter for simulation configurations that satisfy the most OptimalConditions.
            var simulationConfigurationsWithMostOptimalConditionsSatisfied = GetSimulationConfigurationsWithMostOptimalConditionsSatisfied(simulationConfigurations, optimalConditions);

            if (simulationConfigurationsWithMostOptimalConditionsSatisfied.Count == 1)
            {
                return simulationConfigurationsWithMostOptimalConditionsSatisfied.First();
            }

            _logger.LogInformation("{count} simulation configurations remaining after the first filter.", simulationConfigurationsWithMostOptimalConditionsSatisfied.Count);

            // Filter for simulation configurations that optimize the most targeted Properties.
            var simulationConfigurationsWithMostOptimizedProperties = GetSimulationConfigurationsWithMostOptimizedProperties(simulationConfigurationsWithMostOptimalConditionsSatisfied,
                propertyCache);

            _logger.LogInformation("{count} simulation configurations remaining after the second filter.", simulationConfigurationsWithMostOptimizedProperties.Count);

            // At this point, arbitrarily return the first one regardless of the number of simulation configurations remaining.
            return simulationConfigurationsWithMostOptimizedProperties.First();
        }

        private List<SimulationConfiguration> GetSimulationConfigurationsWithMostOptimalConditionsSatisfied(IEnumerable<SimulationConfiguration> simulationConfigurations,
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

        private List<SimulationConfiguration> GetSimulationConfigurationsWithMostOptimizedProperties(IEnumerable<SimulationConfiguration> simulationConfigurations,
            PropertyCache propertyCache)
        {
            var propertyChangesToOptimizeFor = GetPropertyChangesToOptimizeFor(propertyCache);
            var valueHandlers = propertyChangesToOptimizeFor.Select(p => _factory.GetValueHandlerImplementation(p.Property.OwlType));

            _logger.LogInformation("Ordering and filtering simulation results...");
            
            var simulationConfigurationComparer = new SimulationConfigurationComparer(propertyChangesToOptimizeFor.Zip(valueHandlers));

            // Return the simulation configurations with the maximum score.
            return simulationConfigurations.OrderByDescending(s => s, simulationConfigurationComparer)
                .Where(s => simulationConfigurationComparer.Compare(s, simulationConfigurations.First()) > -1)
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

        private void LogOptimalSimulationConfiguration(SimulationConfiguration optimalSimulationConfiguration)
        {
            var logMsg = "Chosen optimal configuration, Actuation actions:\n";

            // Convert to a list to use indexing.
            var simulationTickList = optimalSimulationConfiguration.SimulationTicks.ToList();

            for (var i = 0; i < simulationTickList.Count; i++)
            {
                logMsg += $"Interval {i + 1}:\n";

                foreach (var action in simulationTickList[i].ActuationActions)
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
