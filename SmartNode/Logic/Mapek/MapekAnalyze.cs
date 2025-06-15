using Logic.FactoryInterface;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Models;
using Models.MapekModels;
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

        public Tuple<List<OptimalCondition>, List<Models.Action>> Analyze(IGraph instanceModel, PropertyCache propertyCache)
        {
            _logger.LogInformation("Starting the Analyze phase.");

            var optimalConditions = new List<OptimalCondition>();
            var finalActions = new List<Models.Action>();

            GetRelevantActionsFromUnsatisfiedOptimalConditions(instanceModel, propertyCache, optimalConditions, finalActions);
            GetRelevantActionsFromDesiredOptimizations(instanceModel, propertyCache, finalActions);

            // Filter out duplicate Actions.
            finalActions = finalActions.DistinctBy(x => x.Name).ToList();

            return new Tuple<List<OptimalCondition>, List<Models.Action>>(optimalConditions, finalActions);
        }

        private void GetRelevantActionsFromUnsatisfiedOptimalConditions(IGraph instanceModel,
            PropertyCache propertyCache,
            List<OptimalCondition> optimalConditions,
            List<Models.Action> finalActions)
        {
            var query = MapekUtilities.GetParameterizedStringQuery();

            // Get all OptimalConditions.
            query.CommandText = @"SELECT ?optimalCondition ?property ?reachedInMaximumSeconds WHERE {
                ?optimalCondition rdf:type meta:OptimalCondition .
                ?optimalCondition ssn:forProperty ?property .
                ?optimalCondition meta:reachedInMaximumSeconds ?reachedInMaximumSeconds . }";

            var queryResult = (SparqlResultSet)instanceModel.ExecuteQuery(query);

            // For each OptimalCondition, process its respective constraints and get the appropriate Execution Plans
            // for mitigation.
            foreach (var result in queryResult.Results)
            {
                var optimalConditionNode = result["optimalCondition"];
                var propertyNode = result["property"];
                var propertyName = propertyNode.ToString();
                var reachedInMaximumSeconds = result["reachedInMaximumSeconds"];
                var reachedInMaximumSecondsValue = reachedInMaximumSeconds.ToString().Split('^')[0];
                var propertyType = MapekUtilities.GetPropertyType(instanceModel, propertyNode);

                // Process all the constraints this OptimalCondition might have.
                var constraints = ProcessConstraintQueries(instanceModel, optimalConditionNode, propertyNode, reachedInMaximumSeconds);

                var optimalCondition = new OptimalCondition()
                {
                    Constraints = constraints,
                    ConstraintValueType = propertyType,
                    Name = optimalConditionNode.ToString(),
                    Property = propertyNode.ToString(),
                    ReachedInMaximumSeconds = int.Parse(reachedInMaximumSecondsValue)
                };

                List<Models.Action> actions;

                if (propertyCache.ConfigurableParameters.TryGetValue(propertyName, out ConfigurableParameter configurableParameter))
                {
                    actions = EvaluateConstraintsAndGetActions(instanceModel, optimalCondition, configurableParameter.Value, propertyCache);
                }
                else if (propertyCache.Properties.TryGetValue(propertyName, out Property property))
                {
                    actions = EvaluateConstraintsAndGetActions(instanceModel, optimalCondition, property.Value, propertyCache);
                }
                else
                {
                    _logger.LogError("Property {property} was not found in the system.", propertyName);

                    throw new Exception("The Property must be in the system to be a part of an OptimalCondition.");
                }

                // If there were any unsatisfied constraints, add the current OptimalCondition to the cache.
                if (actions.Count > 0)
                    optimalConditions.Add(optimalCondition);

                finalActions.AddRange(actions);
            }
        }

        private void GetRelevantActionsFromDesiredOptimizations(IGraph instanceModel, PropertyCache propertyCache, List<Models.Action> actions)
        {
            var actuationQuery = MapekUtilities.GetParameterizedStringQuery();

            // Get all ActuationActions, their ActuatorStates, and their Actuators that cause PropertyChanges equal to those that the system
            // wishes to optimize for.
            actuationQuery.CommandText = @"SELECT DISTINCT ?actuationAction ?actuatorState ?actuator WHERE {
                ?actuationAction rdf:type meta:ActuationAction .
                ?actuationAction meta:hasActuatorState ?actuatorState .
                ?actuatorState meta:isActuatorStateOf ?actuator .
                ?actuator rdf:type sosa:Actuator .
                ?actuatorState meta:enacts ?propertyChange .
                ?platform rdf:type sosa:Platform .
                ?platform meta:optimizesFor ?propertyChange . }";

            var actuationQueryResult = (SparqlResultSet)instanceModel.ExecuteQuery(actuationQuery);

            foreach (var result in actuationQueryResult.Results)
            {
                AddActuationActionToCollectionFromQueryResult(result, actions, "actuationAction", "actuatorState", "actuator");
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
                ?platform meta:optimizesFor ?propertyChange . }";

            var reconfigurationQueryResult = (SparqlResultSet)instanceModel.ExecuteQuery(reconfigurationQuery);

            foreach (var result in reconfigurationQueryResult.Results)
            {
                AddReconfigurationActionToCollectionFromQueryResult(result,
                    actions,
                    "reconfigurationAction",
                    "configurableParameter",
                    "effect",
                    propertyCache);
            }
        }

        private List<Tuple<ConstraintOperator, string>> ProcessConstraintQueries(IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds)
        {
            var constraints = new List<Tuple<ConstraintOperator, string>>();

            // Process the constraints from specific queries that check for different kinds of restrictions in OptimalConditions.
            ProcessSingleValueEqualsConstraint(instanceModel, optimalCondition, property, reachedInMaximumSeconds, constraints);
            ProcessFirstValueGreaterThanConstraints(instanceModel, optimalCondition, property, reachedInMaximumSeconds, constraints);
            ProcessFirstValueGreaterThanOrEqualToConstraints(instanceModel, optimalCondition, property, reachedInMaximumSeconds, constraints);
            ProcessFirstValueLessThanConstraints(instanceModel, optimalCondition, property, reachedInMaximumSeconds, constraints);
            ProcessFirstValueLessThanOrEqualToConstraints(instanceModel, optimalCondition, property, reachedInMaximumSeconds, constraints);
            ProcessSecondValueGreaterThanConstraints(instanceModel, optimalCondition, property, reachedInMaximumSeconds, constraints);
            ProcessSecondValueGreaterThanOrEqualToConstraints(instanceModel, optimalCondition, property, reachedInMaximumSeconds, constraints);
            ProcessSecondValueLessThanConstraints(instanceModel, optimalCondition, property, reachedInMaximumSeconds, constraints);
            ProcessSecondValueLessThanOrEqualToConstraints(instanceModel, optimalCondition, property, reachedInMaximumSeconds, constraints);
            ProcessNegatedSingleValueConstraints(instanceModel, optimalCondition, property, reachedInMaximumSeconds, constraints);
            ProcessNegatedFirstValueGreaterThanConstraint(instanceModel, optimalCondition, property, reachedInMaximumSeconds, constraints);
            ProcessNegatedFirstValueGreaterThanOrEqualToConstraint(instanceModel, optimalCondition, property, reachedInMaximumSeconds, constraints);
            ProcessNegatedFirstValueLessThanConstraint(instanceModel, optimalCondition, property, reachedInMaximumSeconds, constraints);
            ProcessNegatedFirstValueLessThanOrEqualToConstraint(instanceModel, optimalCondition, property, reachedInMaximumSeconds, constraints);
            ProcessNegatedSecondValueGreaterThanConstraint(instanceModel, optimalCondition, property, reachedInMaximumSeconds, constraints);
            ProcessNegatedSecondValueGreaterThanOrEqualToConstraint(instanceModel, optimalCondition, property, reachedInMaximumSeconds, constraints);
            ProcessNegatedSecondValueLessThanConstraint(instanceModel, optimalCondition, property, reachedInMaximumSeconds, constraints);
            ProcessNegatedSecondValueLessThanOrEqualToConstraint(instanceModel, optimalCondition, property, reachedInMaximumSeconds, constraints);

            // Return all the constraints that were found.
            return constraints;
        }

        private void ProcessSingleValueEqualsConstraint(IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            List<Tuple<ConstraintOperator, string>> constraints)
        {
            var query = MapekUtilities.GetParameterizedStringQuery();

            // Check if the OptimalCondition has a single-valued constraint.
            query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:hasValue ?constraint . }";

            query.SetParameter("optimalCondition", optimalCondition);
            query.SetParameter("property", property);
            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

            var singleValueQueryResult = (SparqlResultSet)instanceModel.ExecuteQuery(query);

            if (!singleValueQueryResult.IsEmpty)
            {
                // Reasoners (such as Protege's) should only allow one single value constraint per OptimalCondition.
                var constraint = singleValueQueryResult.Results[0]["constraint"].ToString();
                constraint = constraint.Split('^')[0];

                var tuple = new Tuple<ConstraintOperator, string>(ConstraintOperator.EqualTo, constraint);
                constraints.Add(tuple);
            }
        }

        private void ProcessFirstValueGreaterThanConstraints(IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            List<Tuple<ConstraintOperator, string>> constraints)
        {
            var query = MapekUtilities.GetParameterizedStringQuery();

            // Get all first values of constraint ranges with a '>' operator. This kind of query covers both
            // single-valued (e.g., >15) and double-valued (e.g. >15, <25) constraints.
            query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 . 
                    ?bNode3 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:minExclusive ?constraint . }";

            query.SetParameter("optimalCondition", optimalCondition);
            query.SetParameter("property", property);
            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

            ExecuteAndProcessOptimalConditionConstraintQueryResult(instanceModel, query, constraints, ConstraintOperator.GreaterThan);
        }

        private void ProcessFirstValueGreaterThanOrEqualToConstraints(IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            List<Tuple<ConstraintOperator, string>> constraints)
        {
            var query = MapekUtilities.GetParameterizedStringQuery();

            // Get all first values of constraint ranges with a '>=' operator.
            query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 . 
                    ?bNode3 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:minInclusive ?constraint . }";

            query.SetParameter("optimalCondition", optimalCondition);
            query.SetParameter("property", property);
            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

            ExecuteAndProcessOptimalConditionConstraintQueryResult(instanceModel, query, constraints, ConstraintOperator.GreaterThanOrEqualTo);
        }

        private void ProcessFirstValueLessThanConstraints(IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds, 
            List<Tuple<ConstraintOperator, string>> constraints)
        {
            var query = MapekUtilities.GetParameterizedStringQuery();

            // Get all first values of constraint ranges with a '<' operator.
            query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 . 
                    ?bNode3 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:maxExclusive ?constraint . }";

            query.SetParameter("optimalCondition", optimalCondition);
            query.SetParameter("property", property);
            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

            ExecuteAndProcessOptimalConditionConstraintQueryResult(instanceModel, query, constraints, ConstraintOperator.LessThan);
        }

        private void ProcessFirstValueLessThanOrEqualToConstraints(IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            List<Tuple<ConstraintOperator, string>> constraints)
        {
            var query = MapekUtilities.GetParameterizedStringQuery();

            // Get all first values of constraint ranges with a '<=' operator.
            query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 .
                    ?bNode3 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:maxInclusive ?constraint . }";

            query.SetParameter("optimalCondition", optimalCondition);
            query.SetParameter("property", property);
            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

            ExecuteAndProcessOptimalConditionConstraintQueryResult(instanceModel, query, constraints, ConstraintOperator.LessThanOrEqualTo);
        }

        private void ProcessSecondValueGreaterThanConstraints(IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            List<Tuple<ConstraintOperator, string>> constraints)
        {
            var query = MapekUtilities.GetParameterizedStringQuery();

            // Get all second values of constraint ranges with a '>' operator.
            query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 . 
                    ?bNode3 rdf:rest ?bNode4 .
                    ?bNode4 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:minExclusive ?constraint . }";

            query.SetParameter("optimalCondition", optimalCondition);
            query.SetParameter("property", property);
            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

            ExecuteAndProcessOptimalConditionConstraintQueryResult(instanceModel, query, constraints, ConstraintOperator.GreaterThan);
        }

        private void ProcessSecondValueGreaterThanOrEqualToConstraints(IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            List<Tuple<ConstraintOperator, string>> constraints)
        {
            var query = MapekUtilities.GetParameterizedStringQuery();

            // Get all second values of constraint ranges with a '>=' operator.
            query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 . 
                    ?bNode3 rdf:rest ?bNode4 .
                    ?bNode4 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:minInclusive ?constraint . }";

            query.SetParameter("optimalCondition", optimalCondition);
            query.SetParameter("property", property);
            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

            ExecuteAndProcessOptimalConditionConstraintQueryResult(instanceModel, query, constraints, ConstraintOperator.GreaterThanOrEqualTo);
        }

        private void ProcessSecondValueLessThanConstraints(IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            List<Tuple<ConstraintOperator, string>> constraints)
        {
            var query = MapekUtilities.GetParameterizedStringQuery();

            // Get all second values of constraint ranges with a '<' operator.
            query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 . 
                    ?bNode3 rdf:rest ?bNode4 .
                    ?bNode4 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:maxExclusive ?constraint . }";

            query.SetParameter("optimalCondition", optimalCondition);
            query.SetParameter("property", property);
            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

            ExecuteAndProcessOptimalConditionConstraintQueryResult(instanceModel, query, constraints, ConstraintOperator.LessThan);
        }

        private void ProcessSecondValueLessThanOrEqualToConstraints(IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            List<Tuple<ConstraintOperator, string>> constraints)
        {
            var query = MapekUtilities.GetParameterizedStringQuery();

            // Get all second values of constraint ranges with a '<=' operator.
            query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 . 
                    ?bNode3 rdf:rest ?bNode4 .
                    ?bNode4 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:maxInclusive ?constraint . }";

            query.SetParameter("optimalCondition", optimalCondition);
            query.SetParameter("property", property);
            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

            ExecuteAndProcessOptimalConditionConstraintQueryResult(instanceModel, query, constraints, ConstraintOperator.LessThanOrEqualTo);
        }

        private void ProcessNegatedSingleValueConstraints(IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            List<Tuple<ConstraintOperator, string>> constraints)
        {
            var query = MapekUtilities.GetParameterizedStringQuery();

            // Get all negated single value constraints.
            query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:complementOf ?bNode2 .
                    ?bNode2 owl:onProperty meta:hasValueConstraint .
                    ?bNode2 owl:hasValue ?constraint . }";

            query.SetParameter("optimalCondition", optimalCondition);
            query.SetParameter("property", property);
            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

            ExecuteAndProcessOptimalConditionConstraintQueryResult(instanceModel, query, constraints, ConstraintOperator.NotEqualTo);
        }

        private void ProcessNegatedFirstValueGreaterThanConstraint(IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            List<Tuple<ConstraintOperator, string>> constraints)
        {
            var query = MapekUtilities.GetParameterizedStringQuery();

            // Get all negated first values of constraint ranges with a '>' operator. This kind of query covers both
            // single-valued (e.g., not(>15)) and double-valued (e.g. not(>15, <25)) constraints.
            query.CommandText = @"SELECT ?constraint WHERE {
                @optimalCondition ssn:forProperty @property .
                @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                @optimalCondition rdf:type ?bNode1 .
                ?bNode1 owl:complementOf ?bNode2 .
                ?bNode2 owl:onProperty meta:hasValueConstraint .
                ?bNode2 owl:onDataRange ?bNode3 .
                ?bNode3 owl:withRestrictions ?bNode4 .
                ?bNode4 rdf:first ?anonymousNode .
                ?anonymousNode xsd:minExclusive ?constraint . }";

            query.SetParameter("optimalCondition", optimalCondition);
            query.SetParameter("property", property);
            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

            ExecuteAndProcessOptimalConditionConstraintQueryResult(instanceModel, query, constraints, ConstraintOperator.LessThanOrEqualTo);
        }

        private void ProcessNegatedFirstValueGreaterThanOrEqualToConstraint(IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            List<Tuple<ConstraintOperator, string>> constraints)
        {
            var query = MapekUtilities.GetParameterizedStringQuery();

            // Get all negated first values of constraint ranges with a '>=' operator.
            query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:complementOf ?bNode2 .
                    ?bNode2 owl:onProperty meta:hasValueConstraint .
                    ?bNode2 owl:onDataRange ?bNode3 .
                    ?bNode3 owl:withRestrictions ?bNode4 .
                    ?bNode4 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:minInclusive ?constraint . }";

            query.SetParameter("optimalCondition", optimalCondition);
            query.SetParameter("property", property);
            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

            ExecuteAndProcessOptimalConditionConstraintQueryResult(instanceModel, query, constraints, ConstraintOperator.LessThan);
        }

        private void ProcessNegatedFirstValueLessThanConstraint(IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            List<Tuple<ConstraintOperator, string>> constraints)
        {
            var query = MapekUtilities.GetParameterizedStringQuery();

            // Get all negated first values of constraint ranges with a '<' operator.
            query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:complementOf ?bNode2 .
                    ?bNode2 owl:onProperty meta:hasValueConstraint .
                    ?bNode2 owl:onDataRange ?bNode3 .
                    ?bNode3 owl:withRestrictions ?bNode4 .
                    ?bNode4 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:maxExclusive ?constraint . }";

            query.SetParameter("optimalCondition", optimalCondition);
            query.SetParameter("property", property);
            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

            ExecuteAndProcessOptimalConditionConstraintQueryResult(instanceModel, query, constraints, ConstraintOperator.GreaterThanOrEqualTo);
        }

        private void ProcessNegatedFirstValueLessThanOrEqualToConstraint(IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            List<Tuple<ConstraintOperator, string>> constraints)
        {
            var query = MapekUtilities.GetParameterizedStringQuery();

            // Get all negated first values of constraint ranges with a '<=' operator.
            query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:complementOf ?bNode2 .
                    ?bNode2 owl:onProperty meta:hasValueConstraint .
                    ?bNode2 owl:onDataRange ?bNode3 .
                    ?bNode3 owl:withRestrictions ?bNode4 .
                    ?bNode4 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:maxInclusive ?constraint . }";

            query.SetParameter("optimalCondition", optimalCondition);
            query.SetParameter("property", property);
            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

            ExecuteAndProcessOptimalConditionConstraintQueryResult(instanceModel, query, constraints, ConstraintOperator.GreaterThan);
        }

        private void ProcessNegatedSecondValueGreaterThanConstraint(IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            List<Tuple<ConstraintOperator, string>> constraints)
        {
            var query = MapekUtilities.GetParameterizedStringQuery();

            // Get all negated second values of constraint ranges with a '>' operator.
            query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:complementOf ?bNode2 .
                    ?bNode2 owl:onProperty meta:hasValueConstraint .
                    ?bNode2 owl:onDataRange ?bNode3 .
                    ?bNode3 owl:withRestrictions ?bNode4 .
                    ?bNode4 rdf:rest ?bNode5 .
                    ?bNode5 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:minExclusive ?constraint . }";

            query.SetParameter("optimalCondition", optimalCondition);
            query.SetParameter("property", property);
            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

            ExecuteAndProcessOptimalConditionConstraintQueryResult(instanceModel, query, constraints, ConstraintOperator.LessThanOrEqualTo);
        }

        private void ProcessNegatedSecondValueGreaterThanOrEqualToConstraint(IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            List<Tuple<ConstraintOperator, string>> constraints)
        {
            var query = MapekUtilities.GetParameterizedStringQuery();

            // Get all negated second values of constraint ranges with a '>=' operator.
            query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:complementOf ?bNode2 .
                    ?bNode2 owl:onProperty meta:hasValueConstraint .
                    ?bNode2 owl:onDataRange ?bNode3 .
                    ?bNode3 owl:withRestrictions ?bNode4 .
                    ?bNode4 rdf:rest ?bNode5 .
                    ?bNode5 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:minInclusive ?constraint . }";

            query.SetParameter("optimalCondition", optimalCondition);
            query.SetParameter("property", property);
            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

            ExecuteAndProcessOptimalConditionConstraintQueryResult(instanceModel, query, constraints, ConstraintOperator.LessThan);
        }

        private void ProcessNegatedSecondValueLessThanConstraint(IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            List<Tuple<ConstraintOperator, string>> constraints)
        {
            var query = MapekUtilities.GetParameterizedStringQuery();

            // Get all negated second values of constraint ranges with a '<' operator.
            query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:complementOf ?bNode2 .
                    ?bNode2 owl:onProperty meta:hasValueConstraint .
                    ?bNode2 owl:onDataRange ?bNode3 .
                    ?bNode3 owl:withRestrictions ?bNode4 .
                    ?bNode4 rdf:rest ?bNode5 .
                    ?bNode5 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:maxExclusive ?constraint . }";

            query.SetParameter("optimalCondition", optimalCondition);
            query.SetParameter("property", property);
            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

            ExecuteAndProcessOptimalConditionConstraintQueryResult(instanceModel, query, constraints, ConstraintOperator.GreaterThanOrEqualTo);
        }

        private void ProcessNegatedSecondValueLessThanOrEqualToConstraint(IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            List<Tuple<ConstraintOperator, string>> constraints)
        {
            var query = MapekUtilities.GetParameterizedStringQuery();

            // Get all negated second values of constraint ranges with a '<=' operator.
            query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:complementOf ?bNode2 .
                    ?bNode2 owl:onProperty meta:hasValueConstraint .
                    ?bNode2 owl:onDataRange ?bNode3 .
                    ?bNode3 owl:withRestrictions ?bNode4 .
                    ?bNode4 rdf:rest ?bNode5 .
                    ?bNode5 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:maxInclusive ?constraint . }";

            query.SetParameter("optimalCondition", optimalCondition);
            query.SetParameter("property", property);
            query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

            ExecuteAndProcessOptimalConditionConstraintQueryResult(instanceModel, query, constraints, ConstraintOperator.GreaterThan);
        }

        private void ExecuteAndProcessOptimalConditionConstraintQueryResult(IGraph instanceModel,
            SparqlParameterizedString query,
            List<Tuple<ConstraintOperator, string>> constraints,
            ConstraintOperator constraintOperator)
        {
            var queryResult = (SparqlResultSet)instanceModel.ExecuteQuery(query);

            foreach (var innerResult in queryResult)
            {
                var constraint = innerResult["constraint"].ToString();
                constraint = constraint.Split('^')[0];

                var tuple = new Tuple<ConstraintOperator, string>(constraintOperator, constraint);
                constraints.Add(tuple);
            }
        }

        private List<Models.Action> EvaluateConstraintsAndGetActions(IGraph instanceModel,
            OptimalCondition optimalCondition,
            object propertyValue,
            PropertyCache propertyCache)
        {
            var relevantActions = new List<Models.Action>();

            var sensorValueHandler = _factory.GetSensorValueHandlerImplementation(optimalCondition.ConstraintValueType);

            foreach (var constraint in optimalCondition.Constraints)
            {
                // Evaluate the constraint against the current Property value.
                if (!sensorValueHandler.EvaluateConstraint(propertyValue, constraint))
                {
                    _logger.LogInformation(@"OptimalCondition {optimalCondition} with constraint {restriction} {value} is unsatisfied due
                        to Property {property}'s value being {propertyValue}.",
                        optimalCondition.Name,
                        constraint.Item1.ToString(),
                        constraint.Item2,
                        optimalCondition.Property,
                        propertyValue.ToString());

                    // In case of the constraint not being satisfied, get the relevant Actions from the instance model.
                    var actions = GetRelevantActionsFromUnsatisfiedConstraint(instanceModel,
                        optimalCondition.Property,
                        constraint.Item1,
                        propertyCache);

                    relevantActions.AddRange(actions);
                }
            }

            return relevantActions;
        }

        private List<Models.Action> GetRelevantActionsFromUnsatisfiedConstraint(IGraph instanceModel,
            string propertyName,
            ConstraintOperator constraintOperator,
            PropertyCache propertyCache)
        {
            var actions = new List<Models.Action>();

            var filter = string.Empty;

            switch (constraintOperator)
            {
                // In case the unsatisfied constraint is LessThan or LessThanOrEqualTo, any appropriate Action will need
                // to result in a PropertyChange with a ValueDecrease to mitigate it.
                case ConstraintOperator.LessThan:
                case ConstraintOperator.LessThanOrEqualTo:
                    filter = "?propertyChange meta:affectsPropertyWith meta:ValueDecrease .";

                    break;
                // In case the unsatisfied constraint is GreaterThan or GreaterThanOrEqualTo, any appropriate Action will
                // need to result in a PropertyChange with a ValueIncrease to mitigate it.
                case ConstraintOperator.GreaterThan:
                case ConstraintOperator.GreaterThanOrEqualTo:
                    filter = "?propertyChange meta:affectsPropertyWith meta:ValueIncrease .";

                    break;
                // Constraints like Equals and NotEquals can be mitigated through both ValueIncrease and ValueDecrease, so
                // they fall under the default case.
                default:
                    break;
            }

            var actuationQuery = MapekUtilities.GetParameterizedStringQuery();

            // Get all ActuationActions, ActuatorStates, and Actuators that match as relevant Actions given the appropriate
            // filter.
            actuationQuery.CommandText = @"SELECT DISTINCT ?actuationAction ?actuatorState ?actuator WHERE {
                ?actuationAction rdf:type meta:ActuationAction.
                ?actuationAction meta:hasActuatorState ?actuatorState .
                ?actuatorState meta:enacts ?propertyChange .
                ?actuator meta:hasActuatorState ?actuatorState .
                ?actuator rdf:type sosa:Actuator .
                ?propertyChange ssn:forProperty @property .
                " + filter + "}";

            actuationQuery.SetUri("property", new Uri(propertyName));

            var actuationQueryResult = (SparqlResultSet)instanceModel.ExecuteQuery(actuationQuery);

            foreach (var result in actuationQueryResult.Results)
            {
                AddActuationActionToCollectionFromQueryResult(result, actions, "actuationAction", "actuatorState", "actuator");

                _logger.LogInformation("Found ActuationAction {actuationActionName} as a relevant Action.", result["actuationAction"].ToString());
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
                " + filter + "}";

            reconfigurationQuery.SetUri("property", new Uri(propertyName));

            var reconfigurationQueryResult = (SparqlResultSet)instanceModel.ExecuteQuery(reconfigurationQuery);

            foreach (var result in reconfigurationQueryResult.Results)
            {
                AddReconfigurationActionToCollectionFromQueryResult(result, actions, "reconfigurationAction", "configurableParameter", "effect", propertyCache);

                _logger.LogInformation("Found ActuationAction {actuationActionName} as a relevant Action.", result["reconfigurationAction"].ToString());
            }

            return actions;
        }

        private void AddActuationActionToCollectionFromQueryResult(ISparqlResult result,
            List<Models.Action> actions,
            string actuationActionParameter,
            string actuatorStateParameter,
            string actuatorParameter)
        {
            var actuationActionName = result[actuationActionParameter].ToString();
            var actuatorStateName = result[actuatorStateParameter].ToString();
            var actuatorName = result[actuatorParameter].ToString();

            var actuatorState = new ActuatorState
            {
                Actuator = actuatorName,
                Name = actuatorStateName
            };

            var action = new ActuationAction()
            {
                Name = actuationActionName,
                ActuatorState = actuatorState
            };

            actions.Add(action);
        }

        private void AddReconfigurationActionToCollectionFromQueryResult(ISparqlResult result,
            List<Models.Action> actions,
            string actionParameter,
            string configurableParameterParameter,
            string effectParameter,
            PropertyCache propertyCache)
        {
            var reconfigurationActionName = result[actionParameter].ToString();
            var configurableParameterName = result[configurableParameterParameter].ToString();
            var effectName = result[effectParameter].ToString().Split("/")[^1];

            if (!propertyCache.ConfigurableParameters.TryGetValue(configurableParameterName, out ConfigurableParameter configurableParameter))
            {
                _logger.LogError("ConfigurableParameter {configurableParameterName} was not found in the Property cache.", configurableParameterName);

                throw new Exception("ConfigurableParameters must be present in the Property cache after the Monitor phase.");
            }

            if (!Enum.TryParse(effectName, out Effect effect))
            {
                _logger.LogError("Enum value {enumValue} is not supported.", effectName);

                throw new Exception("Parsed string values of PropertyChange Effects must be supported.");
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
