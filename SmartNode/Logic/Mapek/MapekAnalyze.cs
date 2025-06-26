using Logic.FactoryInterface;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Models;
using Models.MapekModels;
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

        public Tuple<List<OptimalCondition>, List<Models.Action>> Analyze(IGraph instanceModel, PropertyCache propertyCache)
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

        private List<OptimalCondition> GetAllOptimalConditions(IGraph instanceModel, PropertyCache propertyCache)
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

                optimalConditions.Add(optimalCondition);
            }

            return optimalConditions;
        }

        private List<OptimalCondition> GetAllUnsatisfiedOptimalConditions(List<OptimalCondition> optimalConditions, PropertyCache propertyCache)
        {
            var unsatisfiedOptimalConditions = new List<OptimalCondition>();

            foreach (var optimalCondition in optimalConditions)
            {
                var sensorValueHandler = _factory.GetSensorValueHandlerImplementation(optimalCondition.ConstraintValueType);
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

                // If a constraint isn't satisfied, add the OptimalCondition to the collection.
                foreach (var constraint in optimalCondition.Constraints)
                {
                    if (!sensorValueHandler.EvaluateConstraint(propertyValue, constraint))
                    {
                        _logger.LogInformation(@"OptimalCondition {optimalCondition} with constraint {restriction} {value} is unsatisfied due
                            to Property {property}'s value being {propertyValue}.",
                            optimalCondition.Name,
                            constraint.Item1.ToString(),
                            constraint.Item2,
                            optimalCondition.Property,
                            propertyValue.ToString());

                        unsatisfiedOptimalConditions.Add(optimalCondition);
                    }
                }
            }

            // Each OptimalCondition might have had multiple unsatisfied constraints, so they could've been added
            // multiple times, so we return a distinct collection.
            return unsatisfiedOptimalConditions.DistinctBy(x => x.Name)
                .ToList();
        }

        private List<Models.Action> GetOptimizationActions(IGraph instanceModel,
            PropertyCache propertyCache,
            List<Models.Action> mitigationActions)
        {
            var actions = new List<Models.Action>();

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
            for (var i = 0; i < mitigationActions.Count; i++)
            {
                if (i == 0)
                {
                    filterStringBuilder.Append("FILTER(?property NOT IN (");
                }

                string propertyName;

                if (mitigationActions[i] is ActuationAction actuationAction)
                {
                    propertyName = actuationAction.ActedOnProperty;
                }
                else
                {
                    propertyName = ((ReconfigurationAction)mitigationActions[i]).ConfigurableParameter.Name;
                }

                // The angle brackets are required around the full Property names to be successfully used
                // in the filter.
                filterStringBuilder.Append('<');
                filterStringBuilder.Append(propertyName);
                filterStringBuilder.Append('>');

                if (i < mitigationActions.Count - 1)
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
            actuationQuery.CommandText = @"SELECT DISTINCT ?actuationAction ?actuatorState ?actuator ?property WHERE {
                ?actuationAction rdf:type meta:ActuationAction .
                ?actuationAction meta:hasActuatorState ?actuatorState .
                ?actuatorState meta:isActuatorStateOf ?actuator .
                ?actuator rdf:type sosa:Actuator .
                ?actuatorState meta:enacts ?propertyChange .
                ?platform rdf:type sosa:Platform .
                ?platform meta:optimizesFor ?propertyChange .
                ?propertyChange ssn:forProperty ?property .
                " + filterString + " }";

            var actuationQueryResult = (SparqlResultSet)instanceModel.ExecuteQuery(actuationQuery);

            foreach (var result in actuationQueryResult.Results)
            {
                AddActuationActionToCollectionFromQueryResult(result, actions);
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
                AddReconfigurationActionToCollectionFromQueryResult(result, actions, propertyCache);
            }

            return actions;
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

            ExecuteAndProcessOptimalConditionConstraintQueryResult(instanceModel, query, constraints, ConstraintOperator.EqualTo);
        }

        private void ProcessFirstValueGreaterThanConstraints(IGraph instanceModel,
            INode optimalCondition,
            INode property,
            INode reachedInMaximumSeconds,
            List<Tuple<ConstraintOperator, string>> constraints)
        {
            var query = MapekUtilities.GetParameterizedStringQuery();

            // Get all first values of constraint ranges with a '>' operator. This kind of query covers both
            // single-valued (e.g., >15) and double-valued (e.g., >15, <25) constraints.
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

        private List<Models.Action> GetMitigationActionsFromUnsatisfiedOptimalConditions(IGraph instanceModel,
            PropertyCache propertyCache,
            List<OptimalCondition> optimalConditions)
        {
            var actions = new List<Models.Action>();

            foreach (var optimalCondition in optimalConditions)
            {
                foreach (var constraint in optimalCondition.Constraints)
                {
                    var filter = string.Empty;

                    switch (constraint.Item1)
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
                    actuationQuery.CommandText = @"SELECT DISTINCT ?actuationAction ?actuatorState ?actuator ?property WHERE {
                        ?actuationAction rdf:type meta:ActuationAction.
                        ?actuationAction meta:hasActuatorState ?actuatorState .
                        ?actuatorState meta:enacts ?propertyChange .
                        ?actuator meta:hasActuatorState ?actuatorState .
                        ?actuator rdf:type sosa:Actuator .
                        ?propertyChange ssn:forProperty ?property .
                        ?property owl:sameAs @property .
                        " + filter + " }";

                    actuationQuery.SetUri("property", new Uri(optimalCondition.Property));

                    var actuationQueryResult = (SparqlResultSet)instanceModel.ExecuteQuery(actuationQuery);

                    foreach (var result in actuationQueryResult.Results)
                    {
                        AddActuationActionToCollectionFromQueryResult(result, actions);

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
                        AddReconfigurationActionToCollectionFromQueryResult(result, actions, propertyCache);

                        _logger.LogInformation("Found ActuationAction {actuationActionName} as a relevant Action.",
                            result["reconfigurationAction"].ToString());
                    }
                }
            }

            // Returns distinct Actions since they're added from every OptimalCondition's unsatisfied constraint.
            return actions.DistinctBy(x => x.Name)
                .ToList();
        }

        private void AddActuationActionToCollectionFromQueryResult(ISparqlResult result, List<Models.Action> actions)
        {
            var actuationActionName = result[0].ToString();
            var actuatorStateName = result[1].ToString();
            var actuatorName = result[2].ToString();
            var propertyName = result[3].ToString();

            var actuatorState = new ActuatorState
            {
                Actuator = actuatorName,
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
            List<Models.Action> actions,
            PropertyCache propertyCache)
        {
            var reconfigurationActionName = result[0].ToString();
            var configurableParameterName = result[1].ToString();
            var effectName = result[2].ToString().Split("/")[^1];

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
