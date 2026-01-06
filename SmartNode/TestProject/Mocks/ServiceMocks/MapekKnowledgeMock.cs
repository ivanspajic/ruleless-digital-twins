using Logic.Mapek;
using Logic.Models.OntologicalModels;
using TestProject.Mocks.Sparql;
using VDS.RDF;
using VDS.RDF.Query;

namespace TestProject.Mocks.ServiceMocks {
    internal class MapekKnowledgeMock : IMapekKnowledge {
        private readonly Dictionary<string, SparqlResultSet> _queryResults = new() {
            { 
                @"SELECT ?actionCombination (GROUP_CONCAT(?action; SEPARATOR="" "") AS ?actions) WHERE {
	                ?actionCombination rdf:type meta:ActionCombination .
	                FILTER NOT EXISTS {
		                {
			                ?actionCombination rdf:comment ""duplicate""^^<http://www.w3.org/2001/XMLSchema#string> .
		                }
		                UNION
		                {
			                ?actionCombination rdf:comment ""not final""^^<http://www.w3.org/2001/XMLSchema#string> .
		                }
	                }
	                ?actionCombination meta:hasActions ?actionList .
	                ?actionList rdf:rest*/rdf:first ?action . }
                GROUP BY ?actionCombination",
                new SparqlResultSet(new List<ISparqlResult> {
                    new SparqlResultMock {
                        Nodes = new Dictionary<string, INode> {
                            { "actions", new NodeFactory().CreateUriNode(new Uri("http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#UnitActuator_0")) }
                        }
                    }
                })
            },
            {
                @"SELECT ?actuator ?actuatorState WHERE {
                        @action rdf:type meta:ActuationAction .
                        @action meta:hasActuator ?actuator .
                        @action meta:hasActuatorState ?actuatorState . }",
                new SparqlResultSet([
                    new SparqlResultMock {
                        Nodes = new Dictionary<string, INode> {
                            { "actuator", new NodeFactory().CreateUriNode(new Uri("http://www.semanticweb.org/vs/ontologies/2025/11/untitled-ontology-97#UnitActuator")) },
                            { "actuatorState", new NodeFactory().CreateLiteralNode("0", new Uri("http://www.w3.org/2001/XMLSchema#int")) }
                        }
                    }
                ])
            }
        };

        public void CommitInMemoryInstanceModelToKnowledgeBase() {
            
        }

        public SparqlResultSet ExecuteQuery(string queryString) {
            throw new NotImplementedException();
        }

        public SparqlResultSet ExecuteQuery(SparqlParameterizedString query, bool useInferredModel = false) {
            if (_queryResults.TryGetValue(query.CommandText, out SparqlResultSet? resultSet)) {
                return resultSet;
            }

            throw new NotImplementedException();
        }

        public SparqlParameterizedString GetParameterizedStringQuery(string queryString) {
            return new SparqlParameterizedString(queryString);
        }

        public string GetPropertyType(string propertyName) {
            throw new NotImplementedException();
        }

        public void LoadModelsFromKnowledgeBase() {
            
        }

        public void UpdateConfigurableParameterValue(ConfigurableParameter configurableParameter) {
            
        }

        public void UpdateModel(SparqlParameterizedString query) {
            
        }

        public void UpdatePropertyValue(Property property) {
            
        }
    }
}
