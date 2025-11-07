using Logic.FactoryInterface;
using Logic.Mapek.Comparers;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VDS.RDF;
using VDS.RDF.Query;

namespace Logic.Mapek
{
    public class MapekAnalyze : IMapekAnalyze
    {
        private readonly ILogger<MapekAnalyze> _logger;
        private readonly IFactory _factory;

        public MapekAnalyze(IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetRequiredService<ILogger<MapekAnalyze>>();
            _factory = serviceProvider.GetRequiredService<IFactory>();
        }

        public Tuple<List<OptimalCondition>, List<Models.OntologicalModels.Action>> Analyze(IGraph instanceModel,
            PropertyCache propertyCache,
            int configurableParameterGranularity)
        {
            _logger.LogInformation("Starting the Analyze phase.");

            var optimalConditions = GetAllOptimalConditions(instanceModel, propertyCache);
            var unsatisfiedOptimalConditions = GetAllUnsatisfiedOptimalConditions(optimalConditions, propertyCache);
            var mitigationActions = GetMitigationActionsFromUnsatisfiedOptimalConditions(instanceModel,
                propertyCache,
                unsatisfiedOptimalConditions);
            var optimizationActions = GetOptimizationActions(instanceModel, propertyCache);

            // Combine the Action collections into one.
            mitigationActions = mitigationActions.Concat(optimizationActions).ToList();
            // Filter out duplicates.
            mitigationActions = mitigationActions.Distinct(new ActionEqualityComparer()).ToList();

            return new (optimalConditions, mitigationActions);
        }

        private List<OptimalCondition> GetAllOptimalConditions(IGraph instanceModel, PropertyCache propertyCache)
        {
            var optimalConditions = new List<OptimalCondition>();

            var query = MapekUtilities.GetParameterizedStringQuery();

            // Get all OptimalConditions.
            query.CommandText = @"SELECT ?optimalCondition ?property ?reachedInMaximumSeconds WHERE {
                ?optimalCondition rdf:type meta:OptimalCondition .
                ?optimalCondition ssn:forProperty ?property .
                ?optimalCondition meta:reachedInMaximumSeconds ?reachedInMaximumSeconds . }";

            var queryResult = instanceModel.ExecuteQuery(query, _logger);

            // For each OptimalCondition, process its respective constraints and get the appropriate Actions
            // for mitigation.
            foreach (var result in queryResult.Results)
            {
                var optimalConditionNode = result["optimalCondition"];
                var propertyNode = result["property"];
                var propertyName = propertyNode.ToString();

                Property property = null!;

                if (propertyCache.Properties.ContainsKey(propertyName))
                {
                    property = propertyCache.Properties[propertyName];
                }
                else if (propertyCache.ConfigurableParameters.ContainsKey(propertyName))
                {
                    property = propertyCache.ConfigurableParameters[propertyName];
                }
                else
                {
                    throw new Exception($"Property {propertyName} not found in the property cache.");
                }

                var reachedInMaximumSeconds = result["reachedInMaximumSeconds"];
                var reachedInMaximumSecondsValue = reachedInMaximumSeconds.ToString().Split('^')[0];

                // Build this OptimalCondition's full expression tree.
                var constraints = GetOptimalConditionConstraints(instanceModel, optimalConditionNode, propertyNode, reachedInMaximumSeconds) ??
                    throw new Exception($"OptimalCondition {optimalConditionNode.ToString()} has no constraints.");

                var optimalCondition = new OptimalCondition()
                {
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

        private List<OptimalCondition> GetAllUnsatisfiedOptimalConditions(IEnumerable<OptimalCondition> optimalConditions, PropertyCache propertyCache)
        {
            var unsatisfiedOptimalConditions = new List<OptimalCondition>();

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

                // If the OptimalCondition is unsatisfied, add it to the collection.
                foreach (var constraint in optimalCondition.Constraints)
                {
                    var unsatisfiedConstraints = valueHandler.GetUnsatisfiedConstraintsFromEvaluation(constraint, propertyValue);

                    if (unsatisfiedConstraints.Any())
                    {
                        optimalCondition.UnsatisfiedAtomicConstraints = unsatisfiedConstraints;

                        unsatisfiedOptimalConditions.Add(optimalCondition);
                    }

                    foreach (var unsatisfiedConstraint in unsatisfiedConstraints)
                    {
                        _logger.LogInformation("Unsatisfied constraint in OptimalCondition {optimalCondition}:" +
                            " (property {property}) {leftValue} {constraintType} {rightValue}.",
                            optimalCondition.Name,
                            optimalCondition.Property,
                            propertyValue.ToString(),
                            unsatisfiedConstraint.ConstraintType.ToString(),
                            unsatisfiedConstraint.Right.ToString());
                    }
                }
            }

            return unsatisfiedOptimalConditions.DistinctBy(x => x.Name).ToList();
        }

        private List<Models.OntologicalModels.Action> GetOptimizationActions(IGraph instanceModel,
            PropertyCache propertyCache)
        {
            var actions = new List<Models.OntologicalModels.Action>();

            var actuationQuery = MapekUtilities.GetParameterizedStringQuery();

            // Get all ActuationActions and their Actuators that cause PropertyChanges equal to those that the system
            // wishes to optimize for.
            actuationQuery.CommandText = @"SELECT DISTINCT ?actuationAction ?actuator WHERE {
                ?actuationAction rdf:type meta:ActuationAction .
                ?actuationAction meta:hasActuator ?actuator .
                ?actuator rdf:type sosa:Actuator .
                ?actuator meta:enacts ?propertyChange .
                ?platform rdf:type sosa:Platform .
                ?platform meta:optimizesFor ?propertyChange . }";

            var actuationQueryResult = instanceModel.ExecuteQuery(actuationQuery, _logger);

            foreach (var result in actuationQueryResult.Results)
            {
                // Passing in the query parameter names is required since their result order is not guaranteed.
                AddActuationActionToCollectionFromQueryResult(result, actions, "actuationAction", "actuator");
            }

            var reconfigurationQuery = MapekUtilities.GetParameterizedStringQuery();

            // Get all ReconfigurationActions, their ConfigurableParameters, and their Effects that cause PropertyChanges equal to those that the
            // system wishes to optimize for.
            reconfigurationQuery.CommandText = @"SELECT DISTINCT ?reconfigurationAction ?configurableParameter ?effect WHERE {
                ?reconfigurationAction rdf:type meta:ReconfigurationAction .
                ?reconfigurationAction ssn:forProperty ?configurableParameter .
                ?configurableParameter meta:enacts ?propertyChange .
                ?platform meta:optimizesFor ?propertyChange . 
                ?platform rdf:type sosa:Platform .
                ?propertyChange meta:alteredBy ?effect . }";

            var reconfigurationQueryResult = instanceModel.ExecuteQuery(reconfigurationQuery, _logger);

            foreach (var result in reconfigurationQueryResult.Results)
            {
                var effectName = result["effect"].ToString().Split("/")[^1];

                if (!Enum.TryParse(effectName, out Effect effect))
                {
                    throw new Exception($"Enum value {effectName} is not supported.");
                }

                // Passing in the query parameter names is required since their result order is not guaranteed.
                AddReconfigurationActionsToCollectionFromQueryResult(result,
                    actions,
                    propertyCache,
                    effect,
                    "reconfigurationAction",
                    "configurableParameter");
            }

            return actions;
        }

        private List<ConstraintExpression> GetOptimalConditionConstraints(IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds)
        {
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
                instanceModel,
                optimalCondition,
                property,
                reachedInMaximumSeconds,
                operatorFilters);

            AddConstraintsOfSecondRangeValues(constraintExpressions,
                instanceModel,
                optimalCondition,
                property,
                reachedInMaximumSeconds,
                operatorFilters);

            AddConstraintsOfDisjunctionsOfOneAndOne(constraintExpressions,
                instanceModel,
                optimalCondition,
                property,
                reachedInMaximumSeconds,
                operatorFilters);

            // For models created with Protege (and the OWL API), disjunctions containing 1 and then 2 values will be converted to those
            // containing 2 and then 1.
            AddConstraintsOfDisjunctionsOfTwoAndOne(constraintExpressions,
                instanceModel,
                optimalCondition,
                property,
                reachedInMaximumSeconds,
                operatorFilters);

            AddConstraintsOfDisjunctionsOfTwoAndTwo(constraintExpressions,
                instanceModel,
                optimalCondition,
                property,
                reachedInMaximumSeconds,
                operatorFilters);

            return constraintExpressions;
        }

        private void AddConstraintsOfFirstOrOnlyRangeValues(IList<ConstraintExpression> constraintExpressions,
            IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            IEnumerable<ConstraintType> constraintTypes)
        {
            foreach (var constraintType in constraintTypes)
            {
                var operatorFilter = GetOperatorFilterFromConstraintType(constraintType);
                var query = MapekUtilities.GetParameterizedStringQuery();

                // Gets the constraints of first or only values of ranges.
                query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 .
                    ?bNode3 rdf:first [ " + operatorFilter + " ?constraint ] .}";

                query.SetParameter("optimalCondition", optimalCondition);
                query.SetParameter("property", property);
                query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

                var queryResult = instanceModel.ExecuteQuery(query, _logger);

                foreach (var result in queryResult.Results)
                {
                    var constraint = result["constraint"].ToString().Split('^')[0];

                    var constraintExpression = new AtomicConstraintExpression
                    {
                        Right = constraint,
                        ConstraintType = constraintType
                    };

                    constraintExpressions.Add(constraintExpression);
                }
            }
        }

        private void AddConstraintsOfSecondRangeValues(IList<ConstraintExpression> constraintExpressions,
            IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            IEnumerable<ConstraintType> constraintTypes)
        {
            foreach (var constraintType in constraintTypes)
            {
                var operatorFilter = GetOperatorFilterFromConstraintType(constraintType);
                var query = MapekUtilities.GetParameterizedStringQuery();

                // Gets the constraints of the second values of ranges.
                query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 .
                    ?bNode3 rdf:rest ?bNode4 .
                    ?bNode4 rdf:first [ " + operatorFilter + " ?constraint ] . }";

                query.SetParameter("optimalCondition", optimalCondition);
                query.SetParameter("property", property);
                query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

                var queryResult = instanceModel.ExecuteQuery(query, _logger);

                foreach (var result in queryResult.Results)
                {
                    var constraint = result["constraint"].ToString().Split('^')[0];

                    var constraintExpression = new AtomicConstraintExpression
                    {
                        Right = constraint,
                        ConstraintType = constraintType
                    };

                    constraintExpressions.Add(constraintExpression);
                }
            }
        }

        private void AddConstraintsOfDisjunctionsOfOneAndOne(IList<ConstraintExpression> constraintExpressions,
            IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            IEnumerable<ConstraintType> constraintTypes)
        {
            foreach (var constraintType1 in constraintTypes)
            {
                var operatorFilter1 = GetOperatorFilterFromConstraintType(constraintType1);

                foreach (var constraintType2 in constraintTypes)
                {
                    var operatorFilter2 = GetOperatorFilterFromConstraintType(constraintType2);
                    var query = MapekUtilities.GetParameterizedStringQuery();

                    // Gets the constraints of two disjunctive, single-valued ranges.
                    query.CommandText = @"SELECT ?constraint1 ?constraint2 WHERE {
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
                        ?bNode6_2 rdf:rest () . }";

                    query.SetParameter("optimalCondition", optimalCondition);
                    query.SetParameter("property", property);
                    query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

                    var queryResult = instanceModel.ExecuteQuery(query, _logger);

                    foreach (var result in queryResult.Results)
                    {
                        var leftConstraint = result["constraint1"].ToString().Split('^')[0];
                        var rightConstraint = result["constraint2"].ToString().Split('^')[0];

                        var leftConstraintExpression = new AtomicConstraintExpression
                        {
                            Right = leftConstraint,
                            ConstraintType = constraintType1
                        };
                        var rightConstraintExpression = new AtomicConstraintExpression
                        {
                            Right = rightConstraint,
                            ConstraintType = constraintType2
                        };
                        var disjunctiveExpression = new NestedConstraintExpression
                        {
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
            IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            IEnumerable<ConstraintType> constraintTypes)
        {
            foreach (var constraintType1 in constraintTypes)
            {
                var operatorFilter1 = GetOperatorFilterFromConstraintType(constraintType1);

                foreach (var constraintType2 in constraintTypes)
                {
                    var operatorFilter2 = GetOperatorFilterFromConstraintType(constraintType2);

                    foreach (var constraintType3 in constraintTypes)
                    {
                        var operatorFilter3 = GetOperatorFilterFromConstraintType(constraintType3);
                        var query = MapekUtilities.GetParameterizedStringQuery();

                        // Gets the constraints of two disjunctive ranges, one two-valued and the other single-valued.
                        query.CommandText = @"SELECT ?constraint1 ?constraint2 ?constraint3 WHERE {
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
                            ?bNode6_2 rdf:rest () . }";

                        query.SetParameter("optimalCondition", optimalCondition);
                        query.SetParameter("property", property);
                        query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

                        var queryResult = instanceModel.ExecuteQuery(query, _logger);

                        foreach (var result in queryResult.Results)
                        {
                            var leftConstraint1 = result["constraint1"].ToString().Split('^')[0];
                            var leftConstraint2 = result["constraint2"].ToString().Split('^')[0];
                            var rightConstraint = result["constraint3"].ToString().Split('^')[0];

                            var leftConstraintExpression1 = new AtomicConstraintExpression
                            {
                                Right = leftConstraint1,
                                ConstraintType = constraintType1
                            };
                            var leftConstraintExpression2 = new AtomicConstraintExpression
                            {
                                Right = leftConstraint2,
                                ConstraintType = constraintType2
                            };
                            var rightConstraintExpression = new AtomicConstraintExpression
                            {
                                Right = rightConstraint,
                                ConstraintType = constraintType3
                            };
                            var leftConstraintExpression = new NestedConstraintExpression
                            {
                                Left = leftConstraintExpression1,
                                Right = leftConstraintExpression2,
                                ConstraintType = ConstraintType.And
                            };
                            var disjunctiveExpression = new NestedConstraintExpression
                            {
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
            IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            IEnumerable<ConstraintType> constraintTypes)
        {
            foreach (var constraintType1 in constraintTypes)
            {
                var operatorFilter1 = GetOperatorFilterFromConstraintType(constraintType1);

                foreach (var constraintType2 in constraintTypes)
                {
                    var operatorFilter2 = GetOperatorFilterFromConstraintType(constraintType2);

                    foreach (var constraintType3 in constraintTypes)
                    {
                        var operatorFilter3 = GetOperatorFilterFromConstraintType(constraintType3);

                        foreach (var constraintType4 in constraintTypes)
                        {
                            var operatorFilter4 = GetOperatorFilterFromConstraintType(constraintType4);
                            var query = MapekUtilities.GetParameterizedStringQuery();

                            // Gets the constraints of two disjunctive, two-valued ranges.
                            query.CommandText = @"SELECT ?constraint1 ?constraint2 ?constraint3 ?constraint4 WHERE {
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
                                ?bNode7_2 rdf:first [ " + operatorFilter4 + " ?constraint4 ] . }";

                            query.SetParameter("optimalCondition", optimalCondition);
                            query.SetParameter("property", property);
                            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

                            var queryResult = instanceModel.ExecuteQuery(query, _logger);

                            foreach (var result in queryResult.Results)
                            {
                                var leftConstraint1 = result["constraint1"].ToString().Split('^')[0];
                                var leftConstraint2 = result["constraint2"].ToString().Split('^')[0];
                                var rightConstraint1 = result["constraint3"].ToString().Split('^')[0];
                                var rightConstraint2 = result["constraint4"].ToString().Split('^')[0];

                                var leftConstraintExpression1 = new AtomicConstraintExpression
                                {
                                    Right = leftConstraint1,
                                    ConstraintType = constraintType1
                                };
                                var leftConstraintExpression2 = new AtomicConstraintExpression
                                {
                                    Right = leftConstraint2,
                                    ConstraintType = constraintType2
                                };
                                var rightConstraintExpression1 = new AtomicConstraintExpression
                                {
                                    Right = rightConstraint1,
                                    ConstraintType = constraintType3
                                };
                                var rightConstraintExpression2 = new AtomicConstraintExpression
                                {
                                    Right = rightConstraint2,
                                    ConstraintType = constraintType4
                                };
                                var leftConstraintExpression = new NestedConstraintExpression
                                {
                                    Left = leftConstraintExpression1,
                                    Right = leftConstraintExpression2,
                                    ConstraintType = ConstraintType.And
                                };
                                var rightConstraintExpression = new NestedConstraintExpression
                                {
                                    Left = rightConstraintExpression1,
                                    Right = rightConstraintExpression2,
                                    ConstraintType = ConstraintType.And
                                };
                                var disjunctiveExpression = new NestedConstraintExpression
                                {
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

        private static string GetOperatorFilterFromConstraintType(ConstraintType constraintType)
        {
            return constraintType switch
            {
                ConstraintType.GreaterThan => "xsd:minExclusive",
                ConstraintType.GreaterThanOrEqualTo => "xsd:minInclusive",
                ConstraintType.LessThan => "xsd:maxExclusive",
                ConstraintType.LessThanOrEqualTo => "xsd:maxInclusive",
                _ => throw new Exception($"{constraintType} is an invalid comparison operator.")
            };
        }

        private List<Models.OntologicalModels.Action> GetMitigationActionsFromUnsatisfiedOptimalConditions(IGraph instanceModel,
            PropertyCache propertyCache,
            IEnumerable<OptimalCondition> optimalConditions)
        {
            var actions = new List<Models.OntologicalModels.Action>();

            foreach (var optimalCondition in optimalConditions)
            {
                foreach (var unsatisfiedConstraint in optimalCondition.UnsatisfiedAtomicConstraints)
                {
                    var filter = string.Empty;
                    var effect = Effect.ValueDecrease;

                    switch (unsatisfiedConstraint.ConstraintType)
                    {
                        // In case the unsatisfied constraint is LessThan or LessThanOrEqualTo, any appropriate Action will need
                        // to result in a PropertyChange with a ValueDecrease to mitigate it.
                        case ConstraintType.LessThan:
                        case ConstraintType.LessThanOrEqualTo:
                            filter = "meta:ValueDecrease";
                            effect = Effect.ValueDecrease;

                            break;
                        // In case the unsatisfied constraint is GreaterThan or GreaterThanOrEqualTo, any appropriate Action will
                        // need to result in a PropertyChange with a ValueIncrease to mitigate it.
                        case ConstraintType.GreaterThan:
                        case ConstraintType.GreaterThanOrEqualTo:
                            filter = "meta:ValueIncrease";
                            effect = Effect.ValueIncrease;

                            break;
                        default:
                            break;
                    }

                    var actuationQuery = MapekUtilities.GetParameterizedStringQuery();

                    // Get all ActuationActions, ActuatorStates, and Actuators that match as relevant Actions given the appropriate filter.
                    actuationQuery.CommandText = @"SELECT DISTINCT ?actuationAction ?actuator WHERE {
                        ?actuationAction rdf:type meta:ActuationAction.
                        ?actuationAction meta:hasActuator ?actuator .
                        ?actuator meta:enacts ?propertyChange .
                        ?actuator rdf:type sosa:Actuator .
                        ?propertyChange ssn:forProperty @property .
                        ?propertyChange meta:affectsPropertyWith " + filter + " . }";

                    actuationQuery.SetUri("property", new Uri(optimalCondition.Property));

                    var actuationQueryResult = instanceModel.ExecuteQuery(actuationQuery, _logger);

                    foreach (var result in actuationQueryResult.Results)
                    {
                        // Passing in the query parameter names is required since their result order is not guaranteed.
                        AddActuationActionToCollectionFromQueryResult(result,
                            actions,
                            "actuationAction",
                            "actuator");

                        _logger.LogInformation("Found ActuationAction {actuationActionName} as a relevant Action.",
                            result["actuationAction"].ToString());
                    }

                    var reconfigurationQuery = MapekUtilities.GetParameterizedStringQuery();

                    // Get all ReconfigurationActions and ConfigurableParameters that match as relevant Actions given the appropriate filter.
                    reconfigurationQuery.CommandText = @"SELECT DISTINCT ?reconfigurationAction ?configurableParameter WHERE {
                        ?reconfigurationAction rdf:type meta:ReconfigurationAction .
                        ?reconfigurationAction ssn:forProperty ?configurableParameter .
                        ?configurableParameter meta:enacts ?propertyChange .
                        ?propertyChange ssn:forProperty @property . }";

                    reconfigurationQuery.SetUri("property", new Uri(optimalCondition.Property));

                    var reconfigurationQueryResult = instanceModel.ExecuteQuery(reconfigurationQuery, _logger);

                    foreach (var result in reconfigurationQueryResult.Results)
                    {
                        // Passing in the query parameter names is required since their result order is not guaranteed.
                        AddReconfigurationActionsToCollectionFromQueryResult(result,
                            actions,
                            propertyCache,
                            effect,
                            "reconfigurationAction",
                            "configurableParameter");

                        _logger.LogInformation("Found ReconfigurationAction {reconfigurationActionName} as a relevant Action.",
                            result["reconfigurationAction"].ToString());
                    }
                }
            }

            return actions;
        }

        private void AddActuationActionToCollectionFromQueryResult(ISparqlResult result,
            IList<Models.OntologicalModels.Action> actions,
            string actuationActionQueryParameter,
            string actuatorQueryParameter)
        {
            var actuationActionName = result[actuationActionQueryParameter].ToString();
            var actuatorName = result[actuatorQueryParameter].ToString();

            var actuator = new Actuator
            {
                Name = actuatorName
            };

            // Create ActuationActions with new states to set for their respective Actuators. Our implementation currently supports direct,
            // user-provided implementations of ActionValueGenerators, although this could theoretically be an FMU execution, a REST API call,
            // or something else.
            var valueHandler = _factory.GetValueHandlerImplementation("int"); // Let integer values designate Actuator states.
            var possibleValues = valueHandler.GetPossibleValuesForActuationAction(actuator);

            foreach (var possibleValue in possibleValues)
            {
                var actuationAction = new ActuationAction
                {
                    Name = actuationActionName,
                    Actuator = actuator,
                    NewStateValue = possibleValue
                };

                actions.Add(actuationAction);
            }
        }

        private void AddReconfigurationActionsToCollectionFromQueryResult(ISparqlResult result,
            IList<Models.OntologicalModels.Action> actions,
            PropertyCache propertyCache,
            Effect effect,
            string reconfigurationActionQueryParameter,
            string configurableParameterQueryParameter)
        {
            var reconfigurationActionName = result[reconfigurationActionQueryParameter].ToString();
            var configurableParameterName = result[configurableParameterQueryParameter].ToString();

            if (!propertyCache.ConfigurableParameters.TryGetValue(configurableParameterName, out ConfigurableParameter? configurableParameter))
            {
                throw new Exception($"ConfigurableParameter {configurableParameterName} was not found in the Property cache.");
            }

            // Create ReconfigurationActions with new values to set for their respective ConfigurableParameters. Out implementation currently
            // supports direct, user-provided implementations of ActionValueGenerators, although this could theoretically be an FMU execution,
            // a REST API call, or something else.
            var valueHandler = _factory.GetValueHandlerImplementation(configurableParameter.OwlType);
            var possibleValues = valueHandler.GetPossibleValuesForReconfigurationAction(configurableParameter, effect);

            foreach (var possibleValue in possibleValues)
            {
                var reconfigurationAction = new ReconfigurationAction
                {
                    ConfigurableParameter = configurableParameter,
                    Name = reconfigurationActionName,
                    NewParameterValue = possibleValue
                };

                actions.Add(reconfigurationAction);
            }
        }
    }
}
