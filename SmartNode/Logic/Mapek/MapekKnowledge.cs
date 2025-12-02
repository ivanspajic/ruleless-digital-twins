using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using VDS.RDF.Query.Datasets;
using VDS.RDF.Update;
using VDS.RDF.Writing;

namespace Logic.Mapek {
    public class MapekKnowledge : IMapekKnowledge {
        public const string DtPrefix = "meta";
        public const string DtUri = "http://www.semanticweb.org/ivans/ontologies/2025/ruleless-digital-twins/";
        public const string SosaPrefix = "sosa";
        public const string SosaUri = "http://www.w3.org/ns/sosa/";
        public const string SsnPrefix = "ssn";
        public const string SsnUri = "http://www.w3.org/ns/ssn/";
        public const string RdfPrefix = "rdf";
        public const string RdfUri = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        public const string OwlPrefix = "owl";
        public const string OwlUri = "http://www.w3.org/2002/07/owl#";
        public const string XsdPrefix = "xsd";
        public const string XsdUri = "http://www.w3.org/2001/XMLSchema#";

        private readonly ILogger<IMapekKnowledge> _logger;

        private readonly IGraph _instanceModel;
        private readonly string _instanceModelFilepath;
        private readonly CompressingTurtleWriter _turtleWriter;

        public MapekKnowledge(IServiceProvider serviceProvider, string instanceModelFilepath) {
            _logger = serviceProvider.GetRequiredService<ILogger<IMapekKnowledge>>();
            _instanceModelFilepath = instanceModelFilepath;
            _turtleWriter = new CompressingTurtleWriter();

            _instanceModel = new Graph();
            new TurtleParser().Load(_instanceModel, instanceModelFilepath);
            
            // If nothing was loaded, don't start the loop.
            if (_instanceModel.IsEmpty) {
                throw new Exception("There is nothing in the instance model graph.");
            }
        }

        public SparqlParameterizedString GetParameterizedStringQuery(string queryString) {
            var query = new SparqlParameterizedString {
                CommandText = queryString
            };

            // Register the relevant prefixes for the queries to come.
            query.Namespaces.AddNamespace(DtPrefix, new Uri(DtUri));
            query.Namespaces.AddNamespace(SosaPrefix, new Uri(SosaUri));
            query.Namespaces.AddNamespace(SsnPrefix, new Uri(SsnUri));
            query.Namespaces.AddNamespace(RdfPrefix, new Uri(RdfUri));
            query.Namespaces.AddNamespace(OwlPrefix, new Uri(OwlUri));
            query.Namespaces.AddNamespace(XsdPrefix, new Uri(XsdUri));

            return query;
        }

        public SparqlResultSet ExecuteQuery(string queryString) {
            var query = GetParameterizedStringQuery(queryString);

            return ExecuteQuery(query);
        }

        public SparqlResultSet ExecuteQuery(SparqlParameterizedString query) {
            var queryResult = (SparqlResultSet)_instanceModel.ExecuteQuery(query);
            _logger.LogInformation("Executed query: {query} ({numResults})", query.CommandText, queryResult.Results.Count);

            if (!queryResult.IsEmpty) {
                var resultString = string.Join("\n", queryResult.Results.Select(r => r.ToString()));
                _logger.LogInformation("Query result: {resultString}", resultString);
            }

            return queryResult;
        }

        public string GetPropertyType(string propertyName) {
            // Check ObservableProperties first.
            var query = GetParameterizedStringQuery(@"SELECT ?propertyValue WHERE {
                @property rdf:type sosa:ObservableProperty .
                @property rdf:type ?bNode . 
                ?bNode owl:hasValue ?propertyValue. }");

            query.SetUri("property", new Uri(propertyName));

            var propertyTypeQueryResult = ExecuteQuery(query);

            if (!propertyTypeQueryResult.IsEmpty) {
                return propertyTypeQueryResult.Results[0]["propertyValue"].ToString().Split("^^")[^1];
            }

            // Check ConfigurableParameters next.
            query = GetParameterizedStringQuery(@"SELECT ?propertyValue WHERE {
                @property rdf:type meta:ConfigurableParameter .
                @property rdf:type ?bNode . 
                ?bNode owl:hasValue ?propertyValue. }");

            query.SetUri("property", new Uri(propertyName));

            propertyTypeQueryResult = ExecuteQuery(query);

            if (!propertyTypeQueryResult.IsEmpty) {
                return propertyTypeQueryResult.Results[0]["propertyValue"].ToString().Split("^^")[^1];
            }

            // Check Output Properties last.
            query = GetParameterizedStringQuery(@"SELECT ?propertyValue WHERE {
                @property rdf:type ssn:Property .
                @property rdf:type ssn:Output .
                @property rdf:type ?bNode .
                ?bNode owl:hasValue ?propertyValue. }");

            query.SetUri("property", new Uri(propertyName));

            propertyTypeQueryResult = ExecuteQuery(query);

            if (!propertyTypeQueryResult.IsEmpty) {
                return propertyTypeQueryResult.Results[0]["propertyValue"].ToString().Split("^^")[^1];
            }

            throw new Exception("The property " + propertyName + " was found without a value type.");
        }

        public void UpdatePropertyValue(Property property) {
            // Update ObservableProperties first.
            var query = GetParameterizedStringQuery(@"DELETE {
                    ?bNode owl:hasValue ?oldValue .
                }
                INSERT {
                    ?bNode owl:hasValue @newValue^^@type .
                }
                WHERE {
                    @property rdf:type sosa:ObservableProperty .
                    @property rdf:type ?bNode .
                    ?bNode owl:hasValue ?oldValue .
                }");

            query.SetLiteral("newValue", property.Value.ToString(), false);
            query.SetUri("type", new Uri(property.OwlType));
            query.SetUri("property", new Uri(property.Name));

            var sparqlUpdateParser = new SparqlUpdateParser();
            var inMemoryDataset = new InMemoryDataset(_instanceModel);
            var processor = new LeviathanUpdateProcessor(inMemoryDataset);
            var commandSet = sparqlUpdateParser.ParseFromString(query);

            processor.ProcessCommandSet(commandSet);

            // In case there was no match on an ObservableProperty, try to update a matching Output.
            query = GetParameterizedStringQuery(@"DELETE {
                    ?bNode owl:hasValue ?oldValue .
                }
                INSERT {
                    ?bNode owl:hasValue @newValue^^@type .
                }
                WHERE {
                    @property rdf:type ssn:Property .
                    @property rdf:type ssn:Output .
                    @property rdf:type ?bNode .
                    ?bNode owl:hasValue ?oldValue .
                }");

            query.SetLiteral("newValue", property.Value.ToString(), false);
            query.SetUri("type", new Uri(property.OwlType));
            query.SetUri("property", new Uri(property.Name));

            commandSet = sparqlUpdateParser.ParseFromString(query);

            processor.ProcessCommandSet(commandSet);
        }

        public void UpdateConfigurableParameterValue(ConfigurableParameter configurableParameter) {
            var query = GetParameterizedStringQuery(@"DELETE {
                    ?bNode owl:hasValue ?oldValue .
                }
                INSERT {
                    ?bNode owl:hasValue @newValue^^@type .
                }
                WHERE {
                    @configurableParameter rdf:type meta:ConfigurableParameter .
                    @configurableParameter rdf:type ?bNode .
                    ?bNode owl:hasValue ?oldValue .
                }");

            query.SetLiteral("newValue", configurableParameter.Value.ToString(), false);
            query.SetUri("type", new Uri(configurableParameter.OwlType));
            query.SetUri("configurableParameter", new Uri(configurableParameter.Name));

            ExecuteQuery(query);
        }

        public void CommitInMemoryInstanceModelToKnowledgeBase() {
            _turtleWriter.Save(_instanceModel, _instanceModelFilepath);
        }

        public FmuModel GetHostPlatformFmuModel(SimulationConfiguration simulationConfiguration, string fmuDirectory) {
            // Retrieve all Actuators to be used in the simulations and ensure that they belong to the same host Platform such that the Platform's
            // FMU will contain all of their relevant input/output variables.
            var actuatorNames = new HashSet<string>();
            var clauseBuilder = new StringBuilder();

            foreach (var simulationTick in simulationConfiguration.SimulationTicks) {
                foreach (var actuationAction in simulationTick.ActuationActions) {
                    if (!actuatorNames.Contains(actuationAction.Actuator.Name)) {
                        actuatorNames.Add(actuationAction.Actuator.Name);

                        // Add the Actuator name to the query filter.
                        clauseBuilder.AppendLine("?platform sosa:hosts <" + actuationAction.Actuator.Name + "> .");
                    }
                }
            }

            var clause = clauseBuilder.ToString();

            var query = GetParameterizedStringQuery(@"SELECT ?fmuModel ?fmuFilePath ?simulationFidelitySeconds WHERE {
                ?platform rdf:type sosa:Platform . " +
                clause +
                @"?platform meta:hasSimulationModel ?fmuModel .
                ?fmuModel rdf:type meta:FmuModel .
                ?fmuModel meta:hasURI ?fmuFilePath .
                ?fmuModel meta:hasSimulationFidelitySeconds ?simulationFidelitySeconds . }");

            var queryResult = ExecuteQuery(query);

            // There can theoretically be multiple Platforms hosting the same Actuator, but we limit ourselves to expect a single Platform
            // per instance model. There should therefore be only one result.
            var fmuModel = queryResult.Results[0];

            return new FmuModel {
                Name = fmuModel["fmuModel"].ToString(),
                FilePath = Path.Combine(fmuDirectory, fmuModel["fmuFilePath"].ToString().Split('^')[0]),
                SimulationFidelitySeconds = int.Parse(fmuModel["simulationFidelitySeconds"].ToString().Split('^')[0])
            };
        }
    }
}
