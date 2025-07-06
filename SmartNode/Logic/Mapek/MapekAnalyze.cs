using Logic.FactoryInterface;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Logic.ValueHandlerInterfaces;
using Lucene.Net.Util;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
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
                var constraints = ProcessConstraintQueries(instanceModel, optimalConditionNode, propertyNode, reachedInMaximumSeconds, property.Value);

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
                        _logger.LogInformation("Unsatisfied constraint in OptimalCondition {optimalCondition}: {unsatisfiedConstraint}.",
                            optimalCondition.Name,
                            unsatisfiedConstraint.ToString());
                    }
                }
            }

            return unsatisfiedOptimalConditions;
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

        private IEnumerable<BinaryExpression> ProcessConstraintQueries(IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            object propertyValue)
        {
            var constraintExpressions = new List<BinaryExpression>();

            // Process the constraints from specific queries that check for different kinds of restrictions in OptimalConditions.
            var equalsConstraint = GetEqualsConstraint(instanceModel,
                optimalCondition,
                property,
                reachedInMaximumSeconds,
                propertyValue);

            // If there is an equals constraint for this Property in this OptimalCondition, then there can't be any other kinds of
            // constraints.
            if (equalsConstraint != null)
            {
                constraintExpressions.Add(equalsConstraint);

                return constraintExpressions;
            }

            AddConstraintsOfFirstOrOnlyValues(constraintExpressions,
                instanceModel,
                optimalCondition,
                property,
                reachedInMaximumSeconds,
                propertyValue);

            AddConstraintsOfTwoRangeValues(constraintExpressions,
                instanceModel,
                optimalCondition,
                property,
                reachedInMaximumSeconds,
                propertyValue);

            AddConstraintsOfDisjunctionsOfOneAndOne(constraintExpressions,
                instanceModel,
                optimalCondition,
                property,
                reachedInMaximumSeconds,
                propertyValue);

            AddConstraintsOfDisjunctionsOfOneAndTwo(constraintExpressions,
                instanceModel,
                optimalCondition,
                property,
                reachedInMaximumSeconds,
                propertyValue);

            AddConstraintsOfDisjunctionsOfTwoAndOne(constraintExpressions,
                instanceModel,
                optimalCondition,
                property,
                reachedInMaximumSeconds,
                propertyValue);

            AddConstraintsOfDisjunctionsOfTwoAndTwo(constraintExpressions,
                instanceModel,
                optimalCondition,
                property,
                reachedInMaximumSeconds,
                propertyValue);

            return constraintExpressions;
        }

        private BinaryExpression? GetEqualsConstraint(IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            object propertyValue)
        {
            BinaryExpression constraints = null!;

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

            // There should be only one such result after validation.
            foreach (var result in queryResult.Results)
            {
                var constraint = queryResult.Results[0]["constraint"].ToString();
                constraint = constraint.Split('^')[0];

                var left = BinaryExpression.Constant(propertyValue);
                var right = BinaryExpression.Constant(constraint);

                constraints = BinaryExpression.Equal(left, right);
            }

            return constraints;
        }

        private void AddConstraintsOfFirstOrOnlyValues(IList<BinaryExpression> constraintExpressions,
            IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            object propertyValue)
        {
            var query = MapekUtilities.GetParameterizedStringQuery();

            query.CommandText = @"SELECT ?bNode3 WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 . }";

            query.SetParameter("optimalCondition", optimalCondition);
            query.SetParameter("property", property);
            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

            var queryResult = (SparqlResultSet)instanceModel.ExecuteQuery(query);

            foreach (var result in queryResult.Results)
            {
                var bNode = result["bNode3"];

                var constraintsFromBNodes = GetConstraintsFromBNodes(instanceModel, propertyValue, bNode);
                constraintExpressions.AddRange(constraintsFromBNodes);
            }
        }

        private void AddConstraintsOfTwoRangeValues(IList<BinaryExpression> constraintExpressions,
            IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            object propertyValue)
        {
            var query = MapekUtilities.GetParameterizedStringQuery();

            query.CommandText = @"SELECT ?bNode3 ?bNode4 WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 .
                    ?bNode3 rdf:rest ?bNode4 . }";

            query.SetParameter("optimalCondition", optimalCondition);
            query.SetParameter("property", property);
            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

            var queryResult = (SparqlResultSet)instanceModel.ExecuteQuery(query);

            foreach (var result in queryResult.Results)
            {
                var lowerLimitBNode = result["bNode3"];
                var upperLimitBNode = result["bNode4"];

                var constraintsFromBNodes = GetConstraintsFromBNodes(instanceModel, propertyValue, lowerLimitBNode, upperLimitBNode);
                constraintExpressions.AddRange(constraintsFromBNodes);
            }
        }

        private void AddConstraintsOfDisjunctionsOfOneAndOne(IList<BinaryExpression> constraintExpressions,
            IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            object propertyValue)
        {
            var query = MapekUtilities.GetParameterizedStringQuery();

            query.CommandText = @"SELECT ?bNode5_1 ?bNode6_2 WHERE {
                @optimalCondition ssn:forProperty @property .
                @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                @optimalCondition rdf:type ?bNode1 .
                ?bNode1 owl:onProperty meta:hasValueConstraint .
                ?bNode1 owl:onDataRange ?bNode2 .
                ?bNode2 owl:unionOf ?bNode3 .
                ?bNode3 rdf:first ?bNode4_1 .
                ?bNode3 rdf:rest ?bNode4_2 .
                ?bNode4_1 owl:withRestrictions ?bNode5_1 .
                ?bNode4_2 rdf:first ?bNode5_2 .
                ?bNode5_2 owl:withRestrictions ?bNode6_2 . }";

            query.SetParameter("optimalCondition", optimalCondition);
            query.SetParameter("property", property);
            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

            var queryResult = (SparqlResultSet)instanceModel.ExecuteQuery(query);

            foreach (var result in queryResult.Results)
            {
                var leftBNode = result["bNode5_1"];
                var rightBNode = result["bNode6_2"];

                // We know there is only one result per bNode.
                var leftExpression = GetConstraintsFromBNodes(instanceModel, propertyValue, leftBNode).First();
                var rightExpression = GetConstraintsFromBNodes(instanceModel, propertyValue, rightBNode).First();

                var disjunctiveExpression = BinaryExpression.Or(leftExpression, rightExpression);

                constraintExpressions.Add(disjunctiveExpression);
            }
        }

        private void AddConstraintsOfDisjunctionsOfOneAndTwo(IList<BinaryExpression> constraintExpressions,
            IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            object propertyValue)
        {
            var query = MapekUtilities.GetParameterizedStringQuery();

            query.CommandText = @"SELECT ?bNode5_1 ?bNode6_2 ?bNode7_2 WHERE {
                @optimalCondition ssn:forProperty @property .
                @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                @optimalCondition rdf:type ?bNode1 .
                ?bNode1 owl:onProperty meta:hasValueConstraint .
                ?bNode1 owl:onDataRange ?bNode2 .
                ?bNode2 owl:unionOf ?bNode3 .
                ?bNode3 rdf:first ?bNode4_1 .
                ?bNode3 rdf:rest ?bNode4_2 .
                ?bNode4_1 owl:withRestrictions ?bNode5_1 .
                ?bNode4_2 rdf:first ?bNode5_2 .
                ?bNode5_2 owl:withRestrictions ?bNode6_2 .
                ?bNode6_2 rdf:rest ?bNode7_2 . }";

            query.SetParameter("optimalCondition", optimalCondition);
            query.SetParameter("property", property);
            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

            var queryResult = (SparqlResultSet)instanceModel.ExecuteQuery(query);

            foreach (var result in queryResult.Results)
            {
                var leftBNode = result["bNode5_1"];
                var rightBNode1 = result["bNode6_2"];
                var rightBNode2 = result["bNode7_2"];

                // We know there is only one result per bNode.
                var leftExpression = GetConstraintsFromBNodes(instanceModel, propertyValue, leftBNode).First();
                var rightExpressions = GetConstraintsFromBNodes(instanceModel, propertyValue, rightBNode1, rightBNode2);
                var finalRightExpression = BuildConjunctiveConstraintExpression(rightExpressions);

                var disjunctiveExpression = BinaryExpression.Or(leftExpression, finalRightExpression);
                constraintExpressions.Add(disjunctiveExpression);
            }
        }

        private void AddConstraintsOfDisjunctionsOfTwoAndOne(IList<BinaryExpression> constraintExpressions,
            IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            object propertyValue)
        {
            var query = MapekUtilities.GetParameterizedStringQuery();

            query.CommandText = @"SELECT ?bNode5_1 ?bNode6_1 ?bNode6_2 WHERE {
                @optimalCondition ssn:forProperty @property .
                @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                @optimalCondition rdf:type ?bNode1 .
                ?bNode1 owl:onProperty meta:hasValueConstraint .
                ?bNode1 owl:onDataRange ?bNode2 .
                ?bNode2 owl:unionOf ?bNode3 .
                ?bNode3 rdf:first ?bNode4_1 .
                ?bNode3 rdf:rest ?bNode4_2 .
                ?bNode4_1 owl:withRestrictions ?bNode5_1 .
                ?bNode5_1 rdf:rest ?bNode6_1 .
                ?bNode4_2 rdf:first ?bNode5_2 .
                ?bNode5_2 owl:withRestrictions ?bNode 6_2 . }";

            query.SetParameter("optimalCondition", optimalCondition);
            query.SetParameter("property", property);
            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

            var queryResult = (SparqlResultSet)instanceModel.ExecuteQuery(query);

            foreach (var result in queryResult.Results)
            {
                var leftBNode1 = result["bNode5_1"];
                var leftBNode2 = result["bNode6_1"];
                var rightBNode = result["bNode6_2"];

                var leftExpressions = GetConstraintsFromBNodes(instanceModel, propertyValue, leftBNode1, leftBNode2);
                // We know there is only one result per bNode.
                var rightExpression = GetConstraintsFromBNodes(instanceModel, propertyValue, rightBNode).First();
                var finalLeftExpression = BuildConjunctiveConstraintExpression(leftExpressions);

                var disjunctiveExpression = BinaryExpression.Or(finalLeftExpression, rightExpression);
                constraintExpressions.Add(disjunctiveExpression);
            }
        }

        private void AddConstraintsOfDisjunctionsOfTwoAndTwo(IList<BinaryExpression> constraintExpressions,
            IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            object propertyValue)
        {
            var query = MapekUtilities.GetParameterizedStringQuery();

            query.CommandText = @"SELECT ?bNode5_1 ?bNode6_1 ?bNode5_2 ?bNode6_2 WHERE {
                @optimalCondition ssn:forProperty @property .
                @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                @optimalCondition rdf:type ?bNode1 .
                ?bNode1 owl:onProperty meta:hasValueConstraint .
                ?bNode1 owl:onDataRange ?bNode2 .
                ?bNode2 owl:unionOf ?bNode3 .
                ?bNode3 rdf:first ?bNode4_1 .
                ?bNode3 rdf:rest ?bNode4_2 .
                ?bNode4_1 owl:withRestrictions ?bNode5_1 .
                ?bNode5_1 rdf:rest ?bNode6_1 .
                ?bNode4_2 rdf:first ?bNode5_2 .
                ?bNode5_2 owl:withRestrictions ?bNode5_2 .
                ?bNode5_2 rdf:rest ?bNode6_2 . }";

            query.SetParameter("optimalCondition", optimalCondition);
            query.SetParameter("property", property);
            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

            var queryResult = (SparqlResultSet)instanceModel.ExecuteQuery(query);

            BinaryExpression finalExpression = null!;

            foreach (var result in queryResult.Results)
            {
                var leftBNode1 = result["bNode5_1"];
                var leftBNode2 = result["bNode6_1"];
                var rightBNode1 = result["bNode5_2"];
                var rightBNode2 = result["bNode6_2"];

                var leftExpressions = GetConstraintsFromBNodes(instanceModel, propertyValue, leftBNode1, leftBNode2);
                var rightExpressions = GetConstraintsFromBNodes(instanceModel, propertyValue, rightBNode1, rightBNode2);
                var finalLeftExpression = BuildConjunctiveConstraintExpression(leftExpressions);
                var finalRightExpression = BuildConjunctiveConstraintExpression(rightExpressions);

                var disjunctiveExpression = BinaryExpression.Or(finalLeftExpression, finalRightExpression);
                constraintExpressions.Add(disjunctiveExpression);
            }
        }

        private IEnumerable<BinaryExpression> GetConstraintsFromBNodes(IGraph instanceModel, object propertyValue, params INode[] bNodes)
        {
            var constraintExpressions = new List<BinaryExpression>();

            foreach (var bNode in bNodes)
            {
                BinaryExpression constraintExpression = null!;

                // Check if the constraint uses a '>' operator.
                var minExclusiveQuery = MapekUtilities.GetParameterizedStringQuery();

                minExclusiveQuery.CommandText = @"SELECT ?constraint WHERE {
                @bNode rdf:first ?anonymousNode .
                ?anonymousNode xsd:minExclusive ?constraint . }";

                minExclusiveQuery.SetParameter("bNode", bNode);

                var minExclusiveQueryResult = (SparqlResultSet)instanceModel.ExecuteQuery(minExclusiveQuery);

                foreach (var result in minExclusiveQueryResult.Results)
                {
                    var constraint = result["constraint"].ToString();
                    constraint = constraint.Split('^')[0];

                    var left = BinaryExpression.Constant(propertyValue);
                    var right = BinaryExpression.Constant(constraint);

                    constraintExpression = BinaryExpression.GreaterThan(left, right);

                    constraintExpressions.Add(constraintExpression);
                }

                // Since there can only be one kind of constraint on the bNode, continue to the next iteration in case it has already been found.
                if (constraintExpression != null)
                {
                    continue;
                }

                // Check if the constraint uses a '>=' operator.
                var minInclusiveQuery = MapekUtilities.GetParameterizedStringQuery();

                minInclusiveQuery.CommandText = @"SELECT ?constraint WHERE {
                @bNode rdf:first ?anonymousNode .
                ?anonymousNode xsd:minInclusive ?constraint . }";

                minInclusiveQuery.SetParameter("bNode", bNode);

                var minInclusiveQueryResult = (SparqlResultSet)instanceModel.ExecuteQuery(minInclusiveQuery);

                foreach (var result in minInclusiveQueryResult.Results)
                {
                    var constraint = result["constraint"].ToString();
                    constraint = constraint.Split('^')[0];

                    var left = BinaryExpression.Constant(propertyValue);
                    var right = BinaryExpression.Constant(constraint);

                    constraintExpression = BinaryExpression.GreaterThanOrEqual(left, right);

                    constraintExpressions.Add(constraintExpression);
                }

                // Since there can only be one kind of constraint on the bNode, continue to the next iteration in case it has already been found.
                if (constraintExpression != null)
                {
                    continue;
                }

                // Check if the constraint uses a '<' operator.
                var maxExclusiveQuery = MapekUtilities.GetParameterizedStringQuery();

                maxExclusiveQuery.CommandText = @"SELECT ?constraint WHERE {
                @bNode rdf:first ?anonymousNode .
                ?anonymousNode xsd:maxExclusive ?constraint . }";

                maxExclusiveQuery.SetParameter("bNode", bNode);

                var maxExclusiveQueryResult = (SparqlResultSet)instanceModel.ExecuteQuery(maxExclusiveQuery);

                foreach (var result in maxExclusiveQueryResult.Results)
                {
                    var constraint = result["constraint"].ToString();
                    constraint = constraint.Split('^')[0];

                    var left = BinaryExpression.Constant(propertyValue);
                    var right = BinaryExpression.Constant(constraint);

                    constraintExpression = BinaryExpression.LessThan(left, right);

                    constraintExpressions.Add(constraintExpression);
                }

                // Since there can only be one kind of constraint on the bNode, continue to the next iteration in case it has already been found.
                if (constraintExpression != null)
                {
                    continue;
                }

                // Check if the constraint uses a '<=' operator.
                var maxInclusiveQuery = MapekUtilities.GetParameterizedStringQuery();

                maxInclusiveQuery.CommandText = @"SELECT ?constraint WHERE {
                @bNode rdf:first ?anonymousNode .
                ?anonymousNode xsd:maxInclusive ?constraint . }";

                maxInclusiveQuery.SetParameter("bNode", bNode);

                var maxInclusiveQueryResult = (SparqlResultSet)instanceModel.ExecuteQuery(maxInclusiveQuery);

                foreach (var result in maxInclusiveQueryResult.Results)
                {
                    var constraint = result["constraint"].ToString();
                    constraint = constraint.Split('^')[0];

                    var left = BinaryExpression.Constant(propertyValue);
                    var right = BinaryExpression.Constant(constraint);

                    constraintExpression = BinaryExpression.LessThan(left, right);

                    constraintExpressions.Add(constraintExpression);
                }
            }

            return constraintExpressions;
        }

        private BinaryExpression BuildConjunctiveConstraintExpression(IEnumerable<BinaryExpression> expressions)
        {
            BinaryExpression finalExpression = null!;

            foreach (var expression in expressions)
            {
                if (finalExpression == null)
                {
                    finalExpression = expression;
                }
                else
                {
                    finalExpression = BinaryExpression.And(expression, finalExpression);
                }
            }

            return finalExpression;
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

                    switch (unsatisfiedConstraint.NodeType)
                    {
                        // In case the unsatisfied constraint is LessThan or LessThanOrEqualTo, any appropriate Action will need
                        // to result in a PropertyChange with a ValueDecrease to mitigate it.
                        case ExpressionType.LessThan:
                        case ExpressionType.LessThanOrEqual:
                            filter = "?propertyChange meta:affectsPropertyWith meta:ValueDecrease .";

                            break;
                        // In case the unsatisfied constraint is GreaterThan or GreaterThanOrEqualTo, any appropriate Action will
                        // need to result in a PropertyChange with a ValueIncrease to mitigate it.
                        case ExpressionType.GreaterThan:
                        case ExpressionType.GreaterThanOrEqual:
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
