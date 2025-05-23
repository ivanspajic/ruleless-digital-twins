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

        public Tuple<List<OptimalCondition>, List<ExecutionPlan>> Analyze(IGraph instanceModel, PropertyCache propertyCache)
        {
            _logger.LogInformation("Starting the Analyze phase.");

            var optimalConditions = new List<OptimalCondition>();
            var executionPlans = new List<ExecutionPlan>();

            var query = MapekUtilities.GetParameterizedStringQuery();

            // Get all OptimalConditions.
            query.CommandText = @"SELECT ?optimalCondition ?property ?reachedInMaximumSeconds WHERE {
                ?optimalCondition rdf:type meta:OptimalCondition .
                ?optimalCondition ssn:forProperty ?property .
                ?optimalCondition meta:reachedInMaximumSeconds ?reachedInMaximumSeconds . }";

            var queryResult = (SparqlResultSet)instanceModel.ExecuteQuery(query);

            foreach (var result in queryResult.Results)
            {
                var optimalCondition = result["optimalCondition"];
                var property = result["property"];
                var reachedInMaximumSeconds = result["reachedInMaximumSeconds"];
                var valueType = MapekUtilities.GetObservablePropertyValueType(instanceModel, property);

                var constraints = ProcessConstraintQueries(instanceModel, optimalCondition, property, reachedInMaximumSeconds);
                //EvaluateConstraints(property, valueType, propertyCache, constraints);
                // find the property value from the caches and use it for evaluation

                // evaluate all the constraints present, and add the optimal condition to the cache
                // in case of at least one constraint not being fulfilled.
                // in case of adding an optimal condition to the cache, query for all execution plans
                // that support regaining optimal conditions   
            }

            // TODO: make the executionplan cache!!
            // query for execution plans that optimize for stuff...

            return new Tuple<List<OptimalCondition>, List<ExecutionPlan>>(optimalConditions, executionPlans);
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

        private void EvaluateConstraints(INode propertyNode,
            string valueType,
            PropertyCache propertyCache,
            List<Tuple<ConstraintOperator, string>> constraints)
        {
            var propertyName = propertyNode.ToString();

            if (propertyCache.ConfigurableParameters.TryGetValue(propertyName, out ConfigurableParameter configurableParameter))
            {

            }
            else if (propertyCache.ComputableProperties.TryGetValue(propertyName, out InputOutput inputOutput))
            {

            }
            else if (propertyCache.ObservableProperties.TryGetValue(propertyName, out ObservableProperty observableProperty))
            {

            }
            else
            {
                _logger.LogError("Property {property} was not found in the system.", propertyName);

                throw new Exception("The Property must be in the system to be a part of an OptimalCondition.");
            }
        }
    }
}
