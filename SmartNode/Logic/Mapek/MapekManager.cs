using Logic.DeviceInterfaces;
using Logic.SensorValueHandlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Models;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;

namespace Logic.Mapek
{
    public class MapekManager : IMapekManager
    {
        private const int SleepyTimeMilliseconds = 5_000;

        private readonly ILogger<MapekManager> _logger;
        private readonly IMapekMonitor _mapekMonitor;
        private readonly IMapekAnalyze _mapekAnalyze;
        private readonly IMapekPlan _mapekPlan;
        private readonly IMapekExecute _mapekExecute;

        private bool _isLoopActive = false;

        public MapekManager(IServiceProvider serviceProvider)
        {
            _logger = serviceProvider.GetRequiredService<ILogger<MapekManager>>();
            _mapekMonitor = serviceProvider.GetRequiredService<IMapekMonitor>();
            _mapekAnalyze = serviceProvider.GetRequiredService<IMapekAnalyze>();
            _mapekPlan = serviceProvider.GetRequiredService<IMapekPlan>();
            _mapekExecute = serviceProvider.GetRequiredService<IMapekExecute>();
        }

        public void StartLoop(string instanceModelFilePath)
        {
            _isLoopActive = true;

            RunMapekLoop(instanceModelFilePath);
        }

        public void StopLoop()
        {
            _isLoopActive = false;
        }

        private void RunMapekLoop(string instanceModelFilePath)
        {
            _logger.LogInformation("Starting the MAPE-K loop.");

            while (_isLoopActive)
            {
                // Load the instance model into a graph object. Doing this inside the loop allows for dynamic model updates at
                // runtime.
                var instanceModel = Initialize(instanceModelFilePath);

                // If nothing was loaded, don't start the loop.
                if (instanceModel.IsEmpty)
                {
                    _logger.LogError("There is nothing in the graph. Terminated MAPE-K loop.");

                    throw new Exception("The graph is empty.");
                }

                // Observe all hard and soft Sensor values.
                var propertyCache = _mapekMonitor.Monitor(instanceModel);
                // Out of all possible ExecutionPlans, filter out the irrelevant ones based on current Property values.
                //var optimalConditionAndExecutionPlanTuple = _mapekAnalyze.Analyze(instanceModel, propertyCache);
                // Plan
                //var executionPlans = _mapekPlan.Plan(optimalConditionAndExecutionPlanTuple.Item1, optimalConditionAndExecutionPlanTuple.Item2);
                // Execute
                //var somethingToReturn = _mapekPlan.Execute(executionPlans);

                Thread.Sleep(SleepyTimeMilliseconds);
            }
        }

        private Graph Initialize(string instanceModelFilePath)
        {
            _logger.LogInformation("Loading instance model file contents from {filePath}.", instanceModelFilePath);

            var instanceModel = new Graph();

            var turtleParser = new TurtleParser();
            try
            {
                turtleParser.Load(instanceModel, instanceModelFilePath);
            }
            catch (Exception exception)
            {    
                _logger.LogError(exception, "Exception while loading file contents: {exceptionMessage}", exception.Message);

                throw;
            }

            return instanceModel;
        }

        #region Analyze

        public void Analyze()
        {
            _logger.LogInformation("Starting the Analyze phase.");

            // Get all OptimalConditions.
            _query.CommandText = @"SELECT ?optimalCondition ?property ?reachedInMaximumSeconds WHERE {
                ?optimalCondition rdf:type meta:OptimalCondition .
                ?optimalCondition ssn:forProperty ?property .
                ?optimalCondition meta:reachedInMaximumSeconds ?reachedInMaximumSeconds . }";

            var queryResult = (SparqlResultSet)_instanceModelGraph.ExecuteQuery(_query);

            foreach (var result in queryResult.Results)
            {
                var optimalCondition = result["optimalCondition"];
                var property = result["property"];
                var reachedInMaximumSeconds = result["reachedInMaximumSeconds"];
                var valueType = GetObservablePropertyValueType(property);

                _query.SetParameter("optimalCondition", optimalCondition);
                _query.SetParameter("property", property);
                _query.SetParameter("reachedInMaximumSeconds", reachedInMaximumSeconds);

                var constraints = ProcessConstraintQueries(result);
                EvaluateConstraints(property, valueType, constraints);
                // find the property value from the caches and use it for evaluation
                
                // evaluate all the constraints present, and add the optimal condition to the cache
                // in case of at least one constraint not being fulfilled.
                // in case of adding an optimal condition to the cache, query for all execution plans
                // that support regaining optimal conditions   
            }

            // TODO: make the executionplan cache!!
            // query for execution plans that optimize for stuff...
        }

        private List<Tuple<ConstraintOperator, string>> ProcessConstraintQueries(ISparqlResult result)
        {
            var constraints = new List<Tuple<ConstraintOperator, string>>();

            // Check if the OptimalCondition has a single-valued constraint.
            ProcessSingleValueEqualsConstraint(constraints);

            // Get all first values of constraint ranges with a '>' operator. This kind of query covers both
            // single-valued (e.g., >15) and double-valued (e.g. >15, <25) constraints.
            ProcessFirstValueGreaterThanConstraints(constraints);

            // Get all first values of constraint ranges with a '>=' operator.
            ProcessFirstValueGreaterThanOrEqualToConstraints(constraints);

            // Get all first values of constraint ranges with a '<' operator.
            ProcessFirstValueLessThanConstraints(constraints);

            // Get all first values of constraint ranges with a '<=' operator.
            ProcessFirstValueLessThanOrEqualToConstraints(constraints);

            // Get all second values of constraint ranges with a '>' operator.
            ProcessSecondValueGreaterThanConstraints(constraints);

            // Get all second values of constraint ranges with a '>=' operator.
            ProcessSecondValueGreaterThanOrEqualToConstraints(constraints);

            // Get all second values of constraint ranges with a '<' operator.
            ProcessSecondValueLessThanConstraints(constraints);

            // Get all second values of constraint ranges with a '<=' operator.
            ProcessSecondValueLessThanOrEqualToConstraints(constraints);

            // Get all negated single value constraints.
            ProcessNegatedSingleValueConstraints(constraints);

            // Get all negated first values of constraint ranges with a '>' operator. This kind of query covers both
            // single-valued (e.g., not(>15)) and double-valued (e.g. not(>15, <25)) constraints.
            ProcessNegatedFirstValueGreaterThanConstraint(constraints);

            // Get all negated first values of constraint ranges with a '>=' operator.
            ProcessNegatedFirstValueGreaterThanOrEqualToConstraint(constraints);

            // Get all negated first values of constraint ranges with a '<' operator.
            ProcessNegatedFirstValueLessThanConstraint(constraints);

            // Get all negated first values of constraint ranges with a '<=' operator.
            ProcessNegatedFirstValueLessThanOrEqualToConstraint(constraints);

            // Get all negated second values of constraint ranges with a '>' operator.
            ProcessNegatedSecondValueGreaterThanConstraint(constraints);

            // Get all negated second values of constraint ranges with a '>=' operator.
            ProcessNegatedSecondValueGreaterThanOrEqualToConstraint(constraints);

            // Get all negated second values of constraint ranges with a '<' operator.
            ProcessNegatedSecondValueLessThanConstraint(constraints);

            // Get all negated second values of constraint ranges with a '<=' operator.
            ProcessNegatedSecondValueLessThanOrEqualToConstraint(constraints);

            return constraints;
        }

        private void ProcessSingleValueEqualsConstraint(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:hasValue ?constraint . }";

            var singleValueQueryResult = (SparqlResultSet)_instanceModelGraph.ExecuteQuery(_query);

            if (!singleValueQueryResult.IsEmpty)
            {
                // Reasoners (such as Protege's) should only allow one single value constraint per OptimalCondition.
                var constraint = singleValueQueryResult.Results[0]["constraint"].ToString();
                constraint = constraint.Split('^')[0];

                var tuple = new Tuple<ConstraintOperator, string>(ConstraintOperator.EqualTo, constraint);
                constraints.Add(tuple);
            }
        }

        private void ProcessFirstValueGreaterThanConstraints(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 . 
                    ?bNode3 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:minExclusive ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.GreaterThan);
        }

        private void ProcessFirstValueGreaterThanOrEqualToConstraints(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 . 
                    ?bNode3 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:minInclusive ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.GreaterThanOrEqualTo);
        }

        private void ProcessFirstValueLessThanConstraints(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 . 
                    ?bNode3 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:maxExclusive ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.LessThan);
        }

        private void ProcessFirstValueLessThanOrEqualToConstraints(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 .
                    ?bNode3 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:maxInclusive ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.LessThanOrEqualTo);
        }

        private void ProcessSecondValueGreaterThanConstraints(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 . 
                    ?bNode3 rdf:rest ?bNode4 .
                    ?bNode4 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:minExclusive ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.GreaterThan);
        }

        private void ProcessSecondValueGreaterThanOrEqualToConstraints(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 . 
                    ?bNode3 rdf:rest ?bNode4 .
                    ?bNode4 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:minInclusive ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.GreaterThanOrEqualTo);
        }

        private void ProcessSecondValueLessThanConstraints(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 . 
                    ?bNode3 rdf:rest ?bNode4 .
                    ?bNode4 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:maxExclusive ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.LessThan);
        }

        private void ProcessSecondValueLessThanOrEqualToConstraints(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:onProperty meta:hasValueConstraint .
                    ?bNode1 owl:onDataRange ?bNode2 .
                    ?bNode2 owl:withRestrictions ?bNode3 . 
                    ?bNode3 rdf:rest ?bNode4 .
                    ?bNode4 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:maxInclusive ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.LessThanOrEqualTo);
        }

        private void ProcessNegatedSingleValueConstraints(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:complementOf ?bNode2 .
                    ?bNode2 owl:onProperty meta:hasValueConstraint .
                    ?bNode2 owl:hasValue ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.NotEqualTo);
        }

        private void ProcessNegatedFirstValueGreaterThanConstraint(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                @optimalCondition ssn:forProperty @property .
                @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                @optimalCondition rdf:type ?bNode1 .
                ?bNode1 owl:complementOf ?bNode2 .
                ?bNode2 owl:onProperty meta:hasValueConstraint .
                ?bNode2 owl:onDataRange ?bNode3 .
                ?bNode3 owl:withRestrictions ?bNode4 .
                ?bNode4 rdf:first ?anonymousNode .
                ?anonymousNode xsd:minExclusive ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.LessThanOrEqualTo);
        }

        private void ProcessNegatedFirstValueGreaterThanOrEqualToConstraint(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:complementOf ?bNode2 .
                    ?bNode2 owl:onProperty meta:hasValueConstraint .
                    ?bNode2 owl:onDataRange ?bNode3 .
                    ?bNode3 owl:withRestrictions ?bNode4 .
                    ?bNode4 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:minInclusive ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.LessThan);
        }

        private void ProcessNegatedFirstValueLessThanConstraint(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:complementOf ?bNode2 .
                    ?bNode2 owl:onProperty meta:hasValueConstraint .
                    ?bNode2 owl:onDataRange ?bNode3 .
                    ?bNode3 owl:withRestrictions ?bNode4 .
                    ?bNode4 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:maxExclusive ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.GreaterThanOrEqualTo);
        }

        private void ProcessNegatedFirstValueLessThanOrEqualToConstraint(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
                    @optimalCondition ssn:forProperty @property .
                    @optimalCondition meta:reachedInMaximumSeconds @reachedInMaximumSeconds .
                    @optimalCondition rdf:type ?bNode1 .
                    ?bNode1 owl:complementOf ?bNode2 .
                    ?bNode2 owl:onProperty meta:hasValueConstraint .
                    ?bNode2 owl:onDataRange ?bNode3 .
                    ?bNode3 owl:withRestrictions ?bNode4 .
                    ?bNode4 rdf:first ?anonymousNode .
                    ?anonymousNode xsd:maxInclusive ?constraint . }";

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.GreaterThan);
        }

        private void ProcessNegatedSecondValueGreaterThanConstraint(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
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

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.LessThanOrEqualTo);
        }

        private void ProcessNegatedSecondValueGreaterThanOrEqualToConstraint(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
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

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.LessThan);
        }

        private void ProcessNegatedSecondValueLessThanConstraint(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
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

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.GreaterThanOrEqualTo);
        }

        private void ProcessNegatedSecondValueLessThanOrEqualToConstraint(List<Tuple<ConstraintOperator, string>> constraints)
        {
            _query.CommandText = @"SELECT ?constraint WHERE {
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

            ExecuteAndProcessOptimalConditionConstraintQueryResult(constraints, ConstraintOperator.GreaterThan);
        }

        private void ExecuteAndProcessOptimalConditionConstraintQueryResult(List<Tuple<ConstraintOperator, string>> constraints,
            ConstraintOperator constraintOperator)
        {
            var queryResult = (SparqlResultSet)_instanceModelGraph.ExecuteQuery(_query);

            foreach (var innerResult in queryResult)
            {
                var constraint = innerResult["constraint"].ToString();
                constraint = constraint.Split('^')[0];

                var tuple = new Tuple<ConstraintOperator, string>(constraintOperator, constraint);
                constraints.Add(tuple);
            }
        }

        private void EvaluateConstraints(INode propertyNode, string valueType, List<Tuple<ConstraintOperator, string>> constraints)
        {
            var propertyName = propertyNode.ToString();

            if (_configurableParameters.TryGetValue(propertyName, out ConfigurableParameter configurableParameter))
            {

            }
            else if (_inputOutputs.TryGetValue(propertyName, out InputOutput inputOutput))
            {

            }
            else if (_observableProperties.TryGetValue(propertyName, out ObservableProperty observableProperty))
            {
                
            }
            else
            {
                _logger.LogError("Property {property} was not found in the system.", propertyName);

                throw new Exception("The Property must be in the system to be a part of an OptimalCondition.");
            }
        }

        #endregion
    }
}
