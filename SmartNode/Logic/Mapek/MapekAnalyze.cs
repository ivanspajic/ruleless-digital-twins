using Logic.FactoryInterface;
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

        public List<Models.OntologicalModels.Action> Analyze(IGraph instanceModel, PropertyCache propertyCache)
        {
            _logger.LogInformation("Starting the Analyze phase.");

            // TODO
            // 1. run the inference on the instance model
            // 2. select the actions from the inferred model and add them to the instance model
            var mitigationActions = GetMitigationActions(instanceModel, propertyCache);

            return mitigationActions;
        }

        private List<Models.OntologicalModels.Action> GetMitigationActions(IGraph instanceModel, PropertyCache propertyCache)
        {
            var actions = new List<Models.OntologicalModels.Action>();

            var actuationQuery = MapekUtilities.GetParameterizedStringQuery();

            // Get all ActuationActions, ActuatorStates, and Actuators that match as relevant Actions.
            actuationQuery.CommandText = @"SELECT ?actuationAction ?actuator WHERE {
                ?actuationAction rdf:type meta:ActuationAction.
                ?actuationAction meta:hasActuator ?actuator .
                ?actuator rdf:type sosa:Actuator . }";

            var actuationQueryResult = instanceModel.ExecuteQuery(actuationQuery, _logger);

            foreach (var result in actuationQueryResult.Results)
            {
                // Passing in the query parameter names is required since their result order is not guaranteed.
                AddActuationActionToCollectionFromQueryResult(result,
                    actions,
                    "actuationAction",
                    "actuator");

                _logger.LogInformation("Found ActuationAction {actuationActionName} as a relevant Action.", result["actuationAction"].ToString());
            }

            var reconfigurationQuery = MapekUtilities.GetParameterizedStringQuery();

            // Get all ReconfigurationActions and ConfigurableParameters that match as relevant Actions.
            reconfigurationQuery.CommandText = @"SELECT ?reconfigurationAction ?configurableParameter WHERE {
                ?reconfigurationAction rdf:type meta:ReconfigurationAction .
                ?reconfigurationAction ssn:forProperty ?configurableParameter .
                ?configurableParameter rdf:type meta:ConfigurableParameter . }";

            var reconfigurationQueryResult = instanceModel.ExecuteQuery(reconfigurationQuery, _logger);

            foreach (var result in reconfigurationQueryResult.Results)
            {
                // Passing in the query parameter names is required since their result order is not guaranteed.
                AddReconfigurationActionsToCollectionFromQueryResult(result,
                    actions,
                    propertyCache,
                    "reconfigurationAction",
                    "configurableParameter");

                _logger.LogInformation("Found ReconfigurationAction {reconfigurationActionName} as a relevant Action.",
                    result["reconfigurationAction"].ToString());
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
            var possibleValues = valueHandler.GetPossibleValuesForReconfigurationAction(configurableParameter);

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
