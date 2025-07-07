using Logic.FactoryInterface;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;
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

        public Tuple<IEnumerable<OptimalCondition>, IEnumerable<Models.OntologicalModels.Action>> Analyze(IGraph instanceModel, PropertyCache propertyCache)
        {
            _logger.LogInformation("Starting the Analyze phase.");

            var optimalConditions = GetAllOptimalConditions(instanceModel, propertyCache);
            var unsatisfiedOptimalConditions = GetAllUnsatisfiedOptimalConditions(optimalConditions, propertyCache);
            var mitigationActions = GetMitigationActionsFromUnsatisfiedOptimalConditions(instanceModel,
                propertyCache,
                unsatisfiedOptimalConditions);
            var optimizationActions = GetOptimizationActions(instanceModel, propertyCache, mitigationActions);

            // Combine the Action collections into one.
            mitigationActions.Concat(optimizationActions);

            return new (optimalConditions, mitigationActions);
        }

        private IEnumerable<OptimalCondition> GetAllOptimalConditions(IGraph instanceModel, PropertyCache propertyCache)
        {
            var optimalConditions = new List<OptimalCondition>();

            var query = MapekUtilities.GetParameterizedStringQuery();

            // Get all OptimalConditions.
            query.CommandText = @"SELECT ?optimalCondition ?property ?reachedInMaximumSeconds WHERE {
                ?optimalCondition rdf:type meta:OptimalCondition .
                ?optimalCondition ssn:forProperty ?property .
                ?optimalCondition meta:reachedInMaximumSeconds ?reachedInMaximumSeconds . }";

            var queryResult = (SparqlResultSet)instanceModel.ExecuteQuery(query);

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
                var constraints = GetOptimalConditionConstraints(instanceModel, optimalConditionNode, propertyNode, reachedInMaximumSeconds, property.Value);

                if (constraints == null)
                {
                    throw new Exception($"OptimalCondition {optimalConditionNode.ToString()} has no constraints.");
                }

                var optimalCondition = new OptimalCondition()
                {
                    Constraints = constraints,
                    ConstraintValueType = property.OwlType,
                    Name = optimalConditionNode.ToString(),
                    Property = propertyName,
                    ReachedInMaximumSeconds = int.Parse(reachedInMaximumSecondsValue)
                };

                optimalConditions.Add(optimalCondition);
            }

            return optimalConditions;
        }

        private IEnumerable<OptimalCondition> GetAllUnsatisfiedOptimalConditions(IEnumerable<OptimalCondition> optimalConditions, PropertyCache propertyCache)
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
                    var unsatisfiedConstraints = valueHandler.GetUnsatisfiedConstraintsFromEvaluation(constraint);

                    if (unsatisfiedConstraints.Any())
                    {
                        optimalCondition.UnsatisfiedAtomicConstraints = unsatisfiedConstraints;

                        unsatisfiedOptimalConditions.Add(optimalCondition);
                    }

                    foreach (var unsatisfiedConstraint in unsatisfiedConstraints)
                    {
                        _logger.LogInformation("Unsatisfied constraint in OptimalCondition {optimalCondition}: {leftValue} {constraintType} {rightValue}.",
                            optimalCondition.Name,
                            unsatisfiedConstraint.Left.ToString(),
                            unsatisfiedConstraint.ConstraintType.ToString(),
                            unsatisfiedConstraint.Right.ToString());
                    }
                }
            }

            return unsatisfiedOptimalConditions.DistinctBy(x => x.Name);
        }

        private IEnumerable<Models.OntologicalModels.Action> GetOptimizationActions(IGraph instanceModel,
            PropertyCache propertyCache,
            IEnumerable<Models.OntologicalModels.Action> mitigationActions)
        {
            var actions = new List<Models.OntologicalModels.Action>();

            // Construct a filter for eliminating Actions that are duplicate or contradicting to those
            // already in the mitigation Action collection.
            //
            // Duplicate Actions are those whose PropertyChanges' Properties and Effects are the same.
            // Contradictory Actions are those whose PropertyChanges' Properties are the same but whose
            // Effects are different. Because there are only two types of Effects (at least in the current
            // version of the ontology), this means that a filter can simply ignore Actions whose
            // PropertyChanges contain the same Properties. This filter thus constructs a set of Properties
            // referenced by the OptimalConditions in the mitigation Action collection.
            var filterStringBuilder = new StringBuilder();

            var firstMitigationAction = mitigationActions.First();
            var lastMitigationAction = mitigationActions.Last();

            foreach (var mitigationAction in mitigationActions)
            {
                if (mitigationAction == firstMitigationAction)
                {
                    filterStringBuilder.Append("FILTER(?property NOT IN (");
                }

                string propertyName;

                if (mitigationAction is ActuationAction actuationAction)
                {
                    propertyName = actuationAction.ActedOnProperty;
                }
                else
                {
                    propertyName = ((ReconfigurationAction)mitigationAction).ConfigurableParameter.Name;
                }

                // The angle brackets are required around the full Property names to be successfully used
                // in the filter.
                filterStringBuilder.Append('<');
                filterStringBuilder.Append(propertyName);
                filterStringBuilder.Append('>');

                if (mitigationAction != lastMitigationAction)
                {
                    filterStringBuilder.Append(", ");
                }
                else
                {
                    filterStringBuilder.Append("))");
                }
            }

            var filterString = filterStringBuilder.ToString();

            var actuationQuery = MapekUtilities.GetParameterizedStringQuery();

            // Get all ActuationActions, their ActuatorStates, and their Actuators that cause PropertyChanges equal to those that the system
            // wishes to optimize for.
            actuationQuery.CommandText = @"SELECT DISTINCT ?actuationAction ?actuatorState ?actuator ?actuatorModel ?property WHERE {
                ?actuationAction rdf:type meta:ActuationAction .
                ?actuationAction meta:hasActuatorState ?actuatorState .
                ?actuatorState meta:isActuatorStateOf ?actuator .
                ?actuator rdf:type sosa:Actuator .
                ?actuator meta:hasModel ?actuatorModel .
                ?actuatorState meta:enacts ?propertyChange .
                ?platform rdf:type sosa:Platform .
                ?platform meta:optimizesFor ?propertyChange .
                ?propertyChange ssn:forProperty ?property .
                " + filterString + " }";

            var actuationQueryResult = (SparqlResultSet)instanceModel.ExecuteQuery(actuationQuery);

            foreach (var result in actuationQueryResult.Results)
            {
                // Passing in the query parameter names is required since their result order is not guaranteed.
                AddActuationActionToCollectionFromQueryResult(result,
                    actions,
                    "actuationAction",
                    "actuatorState",
                    "actuator",
                    "actuatorModel",
                    "property");
            }

            var reconfigurationQuery = MapekUtilities.GetParameterizedStringQuery();

            // Get all ReconfigurationActions, their ConfigurableParameters, and their Effects that cause PropertyChanges equal to those that the
            // system wishes to optimize for.
            reconfigurationQuery.CommandText = @"SELECT DISTINCT ?reconfigurationAction ?configurableParameter ?effect WHERE {
                ?reconfigurationAction rdf:type meta:ReconfigurationAction .
                ?reconfigurationAction ssn:forProperty ?configurableParameter .
                ?reconfigurationAction meta:affectsPropertyWith ?effect .
                ?configurableParameter meta:enacts ?propertyChange .
                ?propertyChange meta:alteredBy ?effect .
                ?platform rdf:type sosa:Platform .
                ?platform meta:optimizesFor ?propertyChange .
                ?propertyChange ssn:forProperty ?property .
                " + filterString + " }";

            var reconfigurationQueryResult = (SparqlResultSet)instanceModel.ExecuteQuery(reconfigurationQuery);

            foreach (var result in reconfigurationQueryResult.Results)
            {
                // Passing in the query parameter names is required since their result order is not guaranteed.
                AddReconfigurationActionToCollectionFromQueryResult(result,
                    actions,
                    propertyCache,
                    "reconfigurationAction",
                    "configurableParameter",
                    "effect");
            }

            return actions;
        }

        private IEnumerable<ConstraintExpression> GetOptimalConditionConstraints(IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            object propertyValue)
        {
            var constraintExpressions = new List<ConstraintExpression>();

            // Process the constraints from specific queries that check for different kinds of restrictions in OptimalConditions.
            AddEqualsConstraint(constraintExpressions,
                    instanceModel,
                    optimalCondition,
                    property,
                    reachedInMaximumSeconds,
                    propertyValue);

            // If there is an equals constraint for this Property in this OptimalCondition, then there can't be any other kinds of
            // constraints.
            if (constraintExpressions.Count > 0)
            {
                return constraintExpressions;
            }

            // TODO: write a comment explaining the need for passing in the collection of constrainttypes due to the impossibility of
            // linking bnodes in sparql (paste the link in here)!!
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
                propertyValue,
                operatorFilters);

            AddConstraintsOfSecondRangeValues(constraintExpressions,
                instanceModel,
                optimalCondition,
                property,
                reachedInMaximumSeconds,
                propertyValue,
                operatorFilters);

            AddConstraintsOfDisjunctionsOfOneAndOne(constraintExpressions,
                instanceModel,
                optimalCondition,
                property,
                reachedInMaximumSeconds,
                propertyValue,
                operatorFilters);

            // For models created with Protege (and the OWL API), disjunctions containing 1 and then 2 values will be converted to those containing 2 and then 1.
            AddConstraintsOfDisjunctionsOfTwoAndOne(constraintExpressions,
                instanceModel,
                optimalCondition,
                property,
                reachedInMaximumSeconds,
                propertyValue,
                operatorFilters);

            AddConstraintsOfDisjunctionsOfTwoAndTwo(constraintExpressions,
                instanceModel,
                optimalCondition,
                property,
                reachedInMaximumSeconds,
                propertyValue,
                operatorFilters);

            return constraintExpressions;
        }

        private void AddEqualsConstraint(IList<ConstraintExpression> constraintExpressions,
            IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            object propertyValue)
        {
            ConstraintExpression constraintExpression = null!;

            var query = MapekUtilities.GetParameterizedStringQuery();

            // Check if the OptimalCondition has a single-valued equals constraint (e.g., =15).
            query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:hasValue ?constraint . }";

            query.SetParameter("optimalCondition", optimalCondition);
            query.SetParameter("property", property);
            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

            var queryResult = (SparqlResultSet)instanceModel.ExecuteQuery(query);

            // There should be a maximum of 1 such result after validation.
            foreach (var result in queryResult.Results)
            {
                var constraint = queryResult.Results[0]["constraint"].ToString();
                constraint = constraint.Split('^')[0];

                constraintExpression = new AtomicConstraintExpression
                {
                    Left = propertyValue,
                    Right = constraint,
                    ConstraintType = ConstraintType.EqualTo
                };

                constraintExpressions.Add(constraintExpression);
            }
        }

        private void AddConstraintsOfFirstOrOnlyRangeValues(IList<ConstraintExpression> constraintExpressions,
            IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            object propertyValue,
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

                var queryResult = (SparqlResultSet)instanceModel.ExecuteQuery(query);

                foreach (var result in queryResult.Results)
                {
                    var constraint = result["constraint"].ToString().Split('^')[0];

                    var constraintExpression = new AtomicConstraintExpression
                    {
                        Left = propertyValue,
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
            object propertyValue,
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

                var queryResult = (SparqlResultSet)instanceModel.ExecuteQuery(query);

                foreach (var result in queryResult.Results)
                {
                    var constraint = result["constraint"].ToString().Split('^')[0];

                    var constraintExpression = new AtomicConstraintExpression
                    {
                        Left = propertyValue,
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
            object propertyValue,
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

                    var queryResult = (SparqlResultSet)instanceModel.ExecuteQuery(query);

                    foreach (var result in queryResult.Results)
                    {
                        var leftConstraint = result["constraint1"].ToString().Split('^')[0];
                        var rightConstraint = result["constraint2"].ToString().Split('^')[0];

                        var leftConstraintExpression = new AtomicConstraintExpression
                        {
                            Left = propertyValue,
                            Right = leftConstraint,
                            ConstraintType = constraintType1
                        };
                        var rightConstraintExpression = new AtomicConstraintExpression
                        {
                            Left = propertyValue,
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
            object propertyValue,
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

                        var queryResult = (SparqlResultSet)instanceModel.ExecuteQuery(query);

                        foreach (var result in queryResult.Results)
                        {
                            var leftConstraint1 = result["constraint1"].ToString().Split('^')[0];
                            var leftConstraint2 = result["constraint2"].ToString().Split('^')[0];
                            var rightConstraint = result["constraint3"].ToString().Split('^')[0];

                            var leftConstraintExpression1 = new AtomicConstraintExpression
                            {
                                Left = propertyValue,
                                Right = leftConstraint1,
                                ConstraintType = constraintType1
                            };
                            var leftConstraintExpression2 = new AtomicConstraintExpression
                            {
                                Left = propertyValue,
                                Right = leftConstraint2,
                                ConstraintType = constraintType2
                            };
                            var rightConstraintExpression = new AtomicConstraintExpression
                            {
                                Left = propertyValue,
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
            object propertyValue,
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

                            var queryResult = (SparqlResultSet)instanceModel.ExecuteQuery(query);

                            foreach (var result in queryResult.Results)
                            {
                                var leftConstraint1 = result["constraint1"].ToString().Split('^')[0];
                                var leftConstraint2 = result["constraint2"].ToString().Split('^')[0];
                                var rightConstraint1 = result["constraint3"].ToString().Split('^')[0];
                                var rightConstraint2 = result["constraint4"].ToString().Split('^')[0];

                                var leftConstraintExpression1 = new AtomicConstraintExpression
                                {
                                    Left = propertyValue,
                                    Right = leftConstraint1,
                                    ConstraintType = constraintType1
                                };
                                var leftConstraintExpression2 = new AtomicConstraintExpression
                                {
                                    Left = propertyValue,
                                    Right = leftConstraint2,
                                    ConstraintType = constraintType2
                                };
                                var rightConstraintExpression1 = new AtomicConstraintExpression
                                {
                                    Left = propertyValue,
                                    Right = rightConstraint1,
                                    ConstraintType = constraintType3
                                };
                                var rightConstraintExpression2 = new AtomicConstraintExpression
                                {
                                    Left = propertyValue,
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

        private string GetOperatorFilterFromConstraintType(ConstraintType constraintType)
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

        private IEnumerable<Models.OntologicalModels.Action> GetMitigationActionsFromUnsatisfiedOptimalConditions(IGraph instanceModel,
            PropertyCache propertyCache,
            IEnumerable<OptimalCondition> optimalConditions)
        {
            var actions = new List<Models.OntologicalModels.Action>();

            foreach (var optimalCondition in optimalConditions)
            {
                foreach (var unsatisfiedConstraint in optimalCondition.UnsatisfiedAtomicConstraints)
                {
                    var filter = string.Empty;

                    switch (unsatisfiedConstraint.ConstraintType)
                    {
                        // In case the unsatisfied constraint is LessThan or LessThanOrEqualTo, any appropriate Action will need
                        // to result in a PropertyChange with a ValueDecrease to mitigate it.
                        case ConstraintType.LessThan:
                        case ConstraintType.LessThanOrEqualTo:
                            filter = "?propertyChange meta:affectsPropertyWith meta:ValueDecrease .";

                            break;
                        // In case the unsatisfied constraint is GreaterThan or GreaterThanOrEqualTo, any appropriate Action will
                        // need to result in a PropertyChange with a ValueIncrease to mitigate it.
                        case ConstraintType.GreaterThan:
                        case ConstraintType.GreaterThanOrEqualTo:
                            filter = "?propertyChange meta:affectsPropertyWith meta:ValueIncrease .";

                            break;
                        // Constraints like Equals through both ValueIncrease and ValueDecrease, so they fall under the default case.
                        default:
                            break;
                    }

                    var actuationQuery = MapekUtilities.GetParameterizedStringQuery();

                    // Get all ActuationActions, ActuatorStates, and Actuators that match as relevant Actions given the appropriate
                    // filter.
                    actuationQuery.CommandText = @"SELECT DISTINCT ?actuationAction ?actuatorState ?actuator ?actuatorModel ?property WHERE {
                        ?actuationAction rdf:type meta:ActuationAction.
                        ?actuationAction meta:hasActuatorState ?actuatorState .
                        ?actuatorState meta:enacts ?propertyChange .
                        ?actuator meta:hasActuatorState ?actuatorState .
                        ?actuator rdf:type sosa:Actuator .
                        ?actuator meta:hasModel ?actuatorModel .
                        ?propertyChange ssn:forProperty ?property .
                        ?property owl:sameAs @property .
                        " + filter + " }";

                    actuationQuery.SetUri("property", new Uri(optimalCondition.Property));

                    var actuationQueryResult = (SparqlResultSet)instanceModel.ExecuteQuery(actuationQuery);

                    foreach (var result in actuationQueryResult.Results)
                    {
                        // Passing in the query parameter names is required since their result order is not guaranteed.
                        AddActuationActionToCollectionFromQueryResult(result,
                            actions,
                            "actuationAction",
                            "actuatorState",
                            "actuator",
                            "actuatorModel",
                            "property");

                        _logger.LogInformation("Found ActuationAction {actuationActionName} as a relevant Action.",
                            result["actuationAction"].ToString());
                    }

                    var reconfigurationQuery = MapekUtilities.GetParameterizedStringQuery();

                    // Get all ReconfigurationActions, ConfigurableParameters, and Effects that match as relevant Actions given the appropriate
                    // filter.
                    reconfigurationQuery.CommandText = @"SELECT DISTINCT ?reconfigurationAction ?configurableParameter ?effect WHERE {
                        ?reconfigurationAction rdf:type meta:ReconfigurationAction .
                        ?reconfigurationAction ssn:forProperty ?configurableParameter .
                        ?reconfigurationAction meta:affectsPropertyWith ?effect .
                        ?configurableParameter meta:enacts ?propertyChange .
                        ?propertyChange ssn:forProperty @property .
                        ?propertyChange meta:alteredBy ?effect .
                        " + filter + " }";

                    reconfigurationQuery.SetUri("property", new Uri(optimalCondition.Property));

                    var reconfigurationQueryResult = (SparqlResultSet)instanceModel.ExecuteQuery(reconfigurationQuery);

                    foreach (var result in reconfigurationQueryResult.Results)
                    {
                        // Passing in the query parameter names is required since their result order is not guaranteed.
                        AddReconfigurationActionToCollectionFromQueryResult(result,
                            actions,
                            propertyCache,
                            "reconfigurationAction",
                            "configurableParameter",
                            "effect");

                        _logger.LogInformation("Found ActuationAction {actuationActionName} as a relevant Action.",
                            result["reconfigurationAction"].ToString());
                    }
                }
            }

            // Returns distinct Actions since they're added from every OptimalCondition's unsatisfied constraint.
            return actions.DistinctBy(x => x.Name);
        }

        private void AddActuationActionToCollectionFromQueryResult(ISparqlResult result,
            IList<Models.OntologicalModels.Action> actions,
            string actuationActionQueryParameter,
            string actuatorStateQueryParameter,
            string actuatorQueryParameter,
            string actuatorModelQueryParameter,
            string propertyQueryParameter)
        {
            var actuationActionName = result[actuationActionQueryParameter].ToString();
            var actuatorStateName = result[actuatorStateQueryParameter].ToString();
            var actuatorName = result[actuatorQueryParameter].ToString();
            var actuatorModel = result[actuatorModelQueryParameter].ToString().Split("^")[0];
            var propertyName = result[propertyQueryParameter].ToString();

            var actuator = new Actuator
            {
                Name = actuatorName,
                Model = actuatorModel
            };

            var actuatorState = new ActuatorState
            {
                Actuator = actuator,
                Name = actuatorStateName
            };

            var actuationAction = new ActuationAction()
            {
                Name = actuationActionName,
                ActuatorState = actuatorState,
                ActedOnProperty = propertyName
            };

            actions.Add(actuationAction);
        }

        private void AddReconfigurationActionToCollectionFromQueryResult(ISparqlResult result,
            IList<Models.OntologicalModels.Action> actions,
            PropertyCache propertyCache,
            string reconfigurationActionQueryParameter,
            string configurableParameterQueryParameter,
            string effectNameQueryParameter)
        {
            var reconfigurationActionName = result[reconfigurationActionQueryParameter].ToString();
            var configurableParameterName = result[configurableParameterQueryParameter].ToString();
            var effectName = result[effectNameQueryParameter].ToString().Split("/")[^1];

            if (!propertyCache.ConfigurableParameters.TryGetValue(configurableParameterName, out ConfigurableParameter configurableParameter))
            {
                throw new Exception($"ConfigurableParameter {configurableParameterName} was not found in the Property cache.");
            }

            if (!Enum.TryParse(effectName, out Effect effect))
            {
                throw new Exception($"Enum value {effectName} is not supported.");
            }

            var reconfigurationAction = new ReconfigurationAction
            {
                ConfigurableParameter = configurableParameter,
                Effect = effect,
                Name = reconfigurationActionName
            };

            actions.Add(reconfigurationAction);
        }
    }
}
