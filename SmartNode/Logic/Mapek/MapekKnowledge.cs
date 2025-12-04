using Logic.FactoryInterface;
using Logic.Models.MapekModels;
using Logic.Models.OntologicalModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
        private readonly IFactory _factory;
        private readonly FilepathArguments _filepathArguments;

        private readonly Graph _instanceModel;
        private readonly Graph _inferredModel;
        private readonly TurtleParser _turtleParser;
        private readonly CompressingTurtleWriter _turtleWriter;

        public MapekKnowledge(IServiceProvider serviceProvider) {
            _logger = serviceProvider.GetRequiredService<ILogger<IMapekKnowledge>>();
            _factory = serviceProvider.GetRequiredService<IFactory>();
            _filepathArguments = serviceProvider.GetRequiredService<FilepathArguments>();
            _turtleWriter = new CompressingTurtleWriter();

            _instanceModel = new Graph();
            _inferredModel = new Graph();
            _turtleParser = new TurtleParser();
            LoadModelsFromKnowledgeFromKnowledgeBase();
            
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

        public SparqlResultSet ExecuteQuery(SparqlParameterizedString query, bool useInferredModel = false) {
            SparqlResultSet queryResult;
            
            if (useInferredModel) {
                queryResult = (SparqlResultSet)_inferredModel.ExecuteQuery(query);
            } else {
                queryResult = (SparqlResultSet)_instanceModel.ExecuteQuery(query);
            }

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
            var valueHandler = _factory.GetValueHandlerImplementation(property.OwlType);
            var propertyValue = valueHandler.GetValueAsCultureInvariantString(property.Value);

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

            query.SetLiteral("newValue", propertyValue, false);
            query.SetUri("type", new Uri(property.OwlType));
            query.SetUri("property", new Uri(property.Name));

            UpdateModel(query);

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

            query.SetLiteral("newValue", propertyValue, false);
            query.SetUri("type", new Uri(property.OwlType));
            query.SetUri("property", new Uri(property.Name));

            UpdateModel(query);
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

            UpdateModel(query);
        }

        public void CommitInMemoryInstanceModelToKnowledgeBase() {
            _turtleWriter.Save(_instanceModel, _filepathArguments.InstanceModelFilepath);
        }

        public void LoadModelsFromKnowledgeFromKnowledgeBase() {
            _instanceModel.Clear();
            _inferredModel.Clear();

            _turtleParser.Load(_instanceModel, _filepathArguments.InstanceModelFilepath);
            _turtleParser.Load(_inferredModel, _filepathArguments.InferredModelFilepath);
        }

        public void UpdateModel(SparqlParameterizedString query) {
            var sparqlUpdateParser = new SparqlUpdateParser();
            var inMemoryDataset = new InMemoryDataset(_instanceModel);
            var processor = new LeviathanUpdateProcessor(inMemoryDataset);
            var commandSet = sparqlUpdateParser.ParseFromString(query);

            processor.ProcessCommandSet(commandSet);
        }
    }
}
